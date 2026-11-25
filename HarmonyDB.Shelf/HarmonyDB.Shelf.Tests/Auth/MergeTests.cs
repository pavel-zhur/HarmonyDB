using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Auth;
using HarmonyDB.Shelf.Model.Me;
using Microsoft.EntityFrameworkCore;
using MusicService = HarmonyDB.Shelf.Database.Model.MusicService;

namespace HarmonyDB.Shelf.Tests.Auth;

public sealed class MergeTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    [DatabaseFact]
    public async Task MergingMovesEverythingAndKeepsTheFirstsOnConflict()
    {
        var first = database.Api.NewBrowser();
        await first.DevAsync("merge-first");
        var second = database.Api.NewBrowser();
        await second.DevAsync("merge-second");

        await using var db = database.CreateContext();
        var firstId = await AccountOf(db, "merge-first");
        var secondId = await AccountOf(db, "merge-second");
        var both = Chords(db, "both");
        var onlySecond = Chords(db, "only-second");
        var firstHeart = Mark(firstId, both);
        var secondHeart = Mark(secondId, both);
        var secondOther = Mark(secondId, onlySecond);
        db.Marks.AddRange(firstHeart, secondHeart, secondOther);
        var playlist = SessionTests.Playlist(secondId, "second's", firstId);
        db.Playlists.Add(playlist);
        db.AccountPermissions.Add(new() { AccountId = secondId, Permission = "mcp.index", GrantedAt = DateTimeOffset.UtcNow });
        db.MusicLinks.AddRange(Link(firstId, MusicService.Yandex), Link(secondId, MusicService.Yandex), Link(secondId, MusicService.Youtube));
        await db.SaveChangesAsync();

        // The first adds the second's sign-in: asked, then merged.
        var asked = await first.DevAsync("merge-second", SignInPurpose.Link);
        Assert.Equal(SignInOutcome.MergeNeeded, asked.Outcome);
        Assert.Equal("merge-second", asked.MergeCandidate!.DisplayName);
        var state = await db.SignInFlows.Where(x => x.Session!.AccountId == firstId && x.CompletedLoginId != null).Select(x => x.State).SingleAsync();
        var question = await first.GetAsync<SignInResult>($"/api/auth/flows/{state}");
        Assert.Equal(["dev"], question.MergeCandidate!.Providers);

        var merged = await first.SendAsync(HttpMethod.Post, $"/api/auth/flows/{state}/merge");
        Assert.Equal(HttpStatusCode.OK, merged.StatusCode);
        Assert.Equal(SignInOutcome.Linked, (await merged.Content.ReadFromJsonAsync<SignInResult>(Browser.Json))!.Outcome);

        db.ChangeTracker.Clear();

        // Sign-ins and the second's sessions.
        Assert.Equal(2, await db.AccountLogins.CountAsync(x => x.AccountId == firstId));
        await Browser.AssertError(await second.SendAsync(HttpMethod.Get, "/api/me"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        var again = database.Api.NewBrowser();
        await again.DevAsync("merge-second");
        Assert.Equal(firstId, (await again.GetAsync<Profile>("/api/me")).Id);

        // Marks: the conflict keeps the first's; the rest moves.
        Assert.Equal(firstId, (await db.Marks.SingleAsync(x => x.Id == firstHeart.Id)).AccountId);
        var tombstoned = await db.Marks.IgnoreQueryFilters().SingleAsync(x => x.Id == secondHeart.Id);
        Assert.Equal(secondId, tombstoned.AccountId);
        Assert.NotNull(tombstoned.DeletedAt);
        Assert.Equal(firstId, (await db.Marks.SingleAsync(x => x.Id == secondOther.Id)).AccountId);

        // The playlist: owned by the first, who was a member; one owner row.
        Assert.Equal(firstId, (await db.Playlists.SingleAsync(x => x.Id == playlist.Id)).OwnerId);
        var members = await db.PlaylistMembers.Where(x => x.PlaylistId == playlist.Id).ToListAsync();
        Assert.Equal(firstId, Assert.Single(members).AccountId);
        Assert.Equal(PlaylistRole.Owner, members[0].Role);

        // Permissions united; music links: the second's only where the first has none, the other purged.
        var permissions = await db.AccountPermissions.Where(x => x.AccountId == firstId).Select(x => x.Permission).ToListAsync();
        Assert.Contains("mcp.index", permissions);
        Assert.Contains("pdf", permissions);
        var links = await db.MusicLinks.Where(x => x.AccountId == firstId).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.Contains(await db.Outbox.Select(x => x.Kind).ToListAsync(), x => x == AudioSubjectPurge.OutboxKind);

        // The second is left deleted; the merge is logged.
        Assert.NotNull((await db.Accounts.SingleAsync(x => x.Id == secondId)).DeletedAt);
        Assert.True(await db.AuditLog.AnyAsync(x => x.AccountId == firstId && x.Action == "accounts_merged"));
    }

    [DatabaseFact]
    public async Task MergingNeedsAFreshSignIn()
    {
        var first = database.Api.NewBrowser();
        await first.DevAsync("stale-first");
        await database.Api.NewBrowser().DevAsync("stale-second");
        await first.DevAsync("stale-second", SignInPurpose.Link);

        await using var db = database.CreateContext();
        var firstId = await AccountOf(db, "stale-first");
        await db.Sessions.Where(x => x.AccountId == firstId)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.SignedInAt, DateTimeOffset.UtcNow.AddHours(-1)));
        var state = await db.SignInFlows.Where(x => x.Session!.AccountId == firstId && x.CompletedLoginId != null).Select(x => x.State).SingleAsync();

        await Browser.AssertError(await first.SendAsync(HttpMethod.Post, $"/api/auth/flows/{state}/merge"), HttpStatusCode.Forbidden, AuthErrorCodes.FreshSignInRequired);
        Assert.Null((await db.Accounts.SingleAsync(x => x.Id == firstId)).DeletedAt);
        Assert.Equal(1, await db.AccountLogins.CountAsync(x => x.AccountId == firstId));
    }

    private static async Task<Guid> AccountOf(ShelfDbContext db, string devName)
        => (await db.AccountLogins.SingleAsync(x => x.Provider == "dev" && x.Subject == devName)).AccountId;

    private static Guid Chords(ShelfDbContext db, string id)
    {
        var chords = new ChordsItem { Id = Guid.NewGuid(), SourceId = $"test:{id}:{Guid.NewGuid():N}", CreatedAt = DateTimeOffset.UtcNow };
        db.Chords.Add(chords);
        return chords.Id;
    }

    private static Mark Mark(Guid accountId, Guid chordsId)
        => new() { Id = Guid.NewGuid(), AccountId = accountId, ChordsId = chordsId, Kind = "heart_red", CreatedAt = DateTimeOffset.UtcNow };

    private static MusicLink Link(Guid accountId, MusicService service) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = accountId,
        Service = service,
        Ciphertext = [1],
        Nonce = [2],
        Tag = [3],
        KeyVersion = 1,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
