using HarmonyDB.Shelf.Database;
using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Auth;
using HarmonyDB.Shelf.Model.Me;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Auth;

public sealed class SessionTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    [DatabaseFact]
    public async Task SignInRotatesTheTokenAndSignOutEnds()
    {
        var browser = database.Api.NewBrowser();
        Assert.Equal(SignInOutcome.SignedIn, (await browser.DevAsync("rotation")).Outcome);
        var first = browser.Cookies["shelf-session"];
        var me = await browser.GetAsync<Profile>("/api/me");
        Assert.Equal("rotation", me.DisplayName);
        Assert.Contains("pdf", me.Permissions);

        await browser.DevAsync("rotation");
        Assert.NotEqual(first, browser.Cookies["shelf-session"]);
        Assert.Single(await browser.GetAsync<List<SessionItem>>("/api/me/sessions"));

        var stale = database.Api.NewBrowser();
        stale.Cookies["shelf-session"] = first;
        await Browser.AssertError(await stale.SendAsync(HttpMethod.Get, "/api/me"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Post, "/api/auth/sign-out")).StatusCode);
        Assert.False(browser.Cookies.ContainsKey("shelf-session"));
        await using var db = database.CreateContext();
        Assert.False(await db.Sessions.AnyAsync(x => x.Account.DisplayName == "rotation"));
    }

    [DatabaseFact]
    public async Task SessionsAreRevokedOneByOneAndAllAtOnce()
    {
        var phone = database.Api.NewBrowser();
        var laptop = database.Api.NewBrowser();
        var tablet = database.Api.NewBrowser();
        await phone.DevAsync("revocation");
        await laptop.DevAsync("revocation");
        await tablet.DevAsync("revocation");

        var sessions = await phone.GetAsync<List<SessionItem>>("/api/me/sessions");
        Assert.Equal(3, sessions.Count);
        Assert.Single(sessions, x => x.Current);

        var laptopSession = (await laptop.GetAsync<List<SessionItem>>("/api/me/sessions")).Single(x => x.Current);
        Assert.Equal(HttpStatusCode.NoContent, (await phone.SendAsync(HttpMethod.Delete, $"/api/me/sessions/{laptopSession.Id}")).StatusCode);
        await Browser.AssertError(await laptop.SendAsync(HttpMethod.Get, "/api/me"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);

        Assert.Equal(HttpStatusCode.NoContent, (await phone.SendAsync(HttpMethod.Delete, "/api/me/sessions")).StatusCode);
        await Browser.AssertError(await tablet.SendAsync(HttpMethod.Get, "/api/me"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        Assert.Single(await phone.GetAsync<List<SessionItem>>("/api/me/sessions"));
    }

    [DatabaseFact]
    public async Task StateChangesOfASessionNeedItsCsrfToken()
    {
        var browser = database.Api.NewBrowser();
        await browser.DevAsync("csrf");
        var settings = await browser.GetAsync<Settings>("/api/me/settings");

        // No header.
        await Browser.AssertError(await browser.SendAsync(HttpMethod.Put, "/api/me/settings", settings, csrf: false), HttpStatusCode.Forbidden, AuthErrorCodes.CsrfFailed);

        // Another session's token, in the cookie and the header both.
        var other = database.Api.NewBrowser();
        await other.DevAsync("csrf-other");
        var own = browser.Cookies["shelf-csrf"];
        browser.Cookies["shelf-csrf"] = other.Cookies["shelf-csrf"];
        await Browser.AssertError(await browser.SendAsync(HttpMethod.Put, "/api/me/settings", settings), HttpStatusCode.Forbidden, AuthErrorCodes.CsrfFailed);

        // The token given with the refusal works.
        Assert.NotEqual(other.Cookies["shelf-csrf"], browser.Cookies["shelf-csrf"]);
        Assert.NotEqual(own, browser.Cookies["shelf-csrf"]);
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Put, "/api/me/settings", settings with { Theme = Theme.Dark })).StatusCode);
        Assert.Equal(Theme.Dark, (await browser.GetAsync<Settings>("/api/me/settings")).Theme);
    }

    [DatabaseFact]
    public async Task ALinkingFlowWorksOnlyInTheSessionThatStartedIt()
    {
        var owner = database.Api.NewBrowser();
        await owner.DevAsync("link-owner");
        var start = await owner.SendAsync(HttpMethod.Post, "/api/auth/flows", new StartSignInRequest { Provider = "dev", Purpose = SignInPurpose.Link });
        var state = (await start.Content.ReadFromJsonAsync<SignInStart>(Browser.Json))!.State;

        // Another person's browser, holding the flow's cookie too.
        var intruder = database.Api.NewBrowser();
        await intruder.DevAsync("link-intruder");
        intruder.Cookies["shelf-flow"] = owner.Cookies["shelf-flow"];
        var answer = new CompleteSignInRequest { Answer = new Dictionary<string, string> { ["name"] = "link-second" } };
        await Browser.AssertError(await intruder.SendAsync(HttpMethod.Post, $"/api/auth/flows/{state}/complete", answer), HttpStatusCode.BadRequest, AuthErrorCodes.FlowInvalid);

        var linked = await owner.SendAsync(HttpMethod.Post, $"/api/auth/flows/{state}/complete", answer);
        Assert.Equal(SignInOutcome.Linked, (await linked.Content.ReadFromJsonAsync<SignInResult>(Browser.Json))!.Outcome);
        Assert.Equal(2, (await owner.GetAsync<List<LoginItem>>("/api/me/logins")).Count);

        // Used once.
        await Browser.AssertError(await owner.SendAsync(HttpMethod.Post, $"/api/auth/flows/{state}/complete", answer), HttpStatusCode.BadRequest, AuthErrorCodes.FlowInvalid);
    }

    [DatabaseFact]
    public async Task GuestsAreAskedToSignIn()
    {
        var guest = database.Api.NewBrowser();
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, "/api/me"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, "/api/me/wipe", csrf: false), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);

        // Signing in itself needs the token, for guests too.
        await Browser.AssertError(
            await guest.SendAsync(HttpMethod.Post, "/api/auth/flows", new StartSignInRequest { Provider = "dev" }, csrf: false),
            HttpStatusCode.Forbidden,
            AuthErrorCodes.CsrfFailed);

        var session = await guest.GetAsync<CurrentSession>("/api/auth/session");
        Assert.Null(session.AccountId);
    }

    [DatabaseFact]
    public async Task DeletingTheProfilePassesSharedPlaylistsOn()
    {
        var owner = database.Api.NewBrowser();
        await owner.DevAsync("leaving-owner");
        var member = database.Api.NewBrowser();
        await member.DevAsync("leaving-member");

        await using var db = database.CreateContext();
        var ownerId = (await db.AccountLogins.SingleAsync(x => x.Subject == "leaving-owner")).AccountId;
        var memberId = (await db.AccountLogins.SingleAsync(x => x.Subject == "leaving-member")).AccountId;
        var shared = Playlist(ownerId, "shared", memberId);
        var alone = Playlist(ownerId, "alone");
        db.Playlists.AddRange(shared, alone);
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(HttpMethod.Delete, "/api/me")).StatusCode);
        await Browser.AssertError(await owner.SendAsync(HttpMethod.Get, "/api/me"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);

        db.ChangeTracker.Clear();
        Assert.Equal(memberId, (await db.Playlists.SingleAsync(x => x.Id == shared.Id)).OwnerId);
        Assert.Equal(PlaylistRole.Owner, (await db.PlaylistMembers.SingleAsync(x => x.PlaylistId == shared.Id)).Role);
        Assert.NotNull((await db.Playlists.IgnoreQueryFilters().SingleAsync(x => x.Id == alone.Id)).DeletedAt);
        var account = await db.Accounts.SingleAsync(x => x.Id == ownerId);
        Assert.NotNull(account.DeletedAt);
        Assert.Equal(string.Empty, account.DisplayName);
        Assert.False(await db.AccountLogins.AnyAsync(x => x.AccountId == ownerId));
    }

    [DatabaseFact]
    public async Task OnlyTheSignInTheAccountWasMadeWithRenamesIt()
    {
        var browser = database.Api.NewBrowser();
        await browser.DevAsync("rename-original");
        await browser.DevAsync("rename-second", SignInPurpose.Link);

        await using var db = database.CreateContext();
        var accountId = (await db.AccountLogins.SingleAsync(x => x.Subject == "rename-original")).AccountId;
        await db.Accounts.Where(x => x.Id == accountId).ExecuteUpdateAsync(x => x.SetProperty(a => a.DisplayName, "A Person"));

        await database.Api.NewBrowser().DevAsync("rename-second");
        Assert.Equal("A Person", (await db.Accounts.SingleAsync(x => x.Id == accountId)).DisplayName);

        await database.Api.NewBrowser().DevAsync("rename-original");
        Assert.Equal("rename-original", (await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == accountId)).DisplayName);
    }

    [DatabaseFact]
    public async Task TheDevSignInAsAnExistingAccountChangesNothing()
    {
        await database.Api.NewBrowser().DevAsync("existing-person");
        await using var db = database.CreateContext();
        var accountId = (await db.AccountLogins.SingleAsync(x => x.Subject == "existing-person")).AccountId;
        await db.Accounts.Where(x => x.Id == accountId).ExecuteUpdateAsync(x => x.SetProperty(a => a.DisplayName, "Imported Person"));

        var browser = database.Api.NewBrowser();
        Assert.Equal(SignInOutcome.SignedIn, (await browser.DevAsAccountAsync(accountId)).Outcome);
        var profile = await browser.GetAsync<Profile>("/api/me");
        Assert.Equal(accountId, profile.Id);
        Assert.Equal("Imported Person", profile.DisplayName);
        Assert.Equal(1, await db.AccountLogins.CountAsync(x => x.AccountId == accountId));
    }

    [DatabaseFact]
    public async Task ChangingSignInsNeedsARecentSignIn()
    {
        var browser = database.Api.NewBrowser();
        await browser.DevAsync("stale-session");
        await browser.DevAsync("stale-session-second", SignInPurpose.Link);
        var second = (await browser.GetAsync<List<LoginItem>>("/api/me/logins")).Single(x => x.Username == "stale-session-second");

        await using var db = database.CreateContext();
        await db.Sessions.Where(x => x.Account.Logins.Any(l => l.Subject == "stale-session"))
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.SignedInAt, DateTimeOffset.UtcNow.AddHours(-1)).SetProperty(s => s.LastSeenAt, DateTimeOffset.UtcNow.AddHours(-1)));

        await Browser.AssertError(
            await browser.SendAsync(HttpMethod.Post, "/api/auth/flows", new StartSignInRequest { Provider = "dev", Purpose = SignInPurpose.Link }),
            HttpStatusCode.Forbidden,
            AuthErrorCodes.FreshSignInRequired);
        await Browser.AssertError(await browser.SendAsync(HttpMethod.Delete, $"/api/me/logins/{second.Id}"), HttpStatusCode.Forbidden, AuthErrorCodes.FreshSignInRequired);

        // Signed in again, it works.
        await browser.DevAsync("stale-session");
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, $"/api/me/logins/{second.Id}")).StatusCode);
    }

    [DatabaseFact]
    public async Task WipingEmptiesTheProfileAndKeepsIt()
    {
        var person = database.Api.NewBrowser();
        await person.DevAsync("wipe-person");
        await database.Api.NewBrowser().DevAsync("wipe-other");

        await using var db = database.CreateContext();
        var me = (await db.AccountLogins.SingleAsync(x => x.Subject == "wipe-person")).AccountId;
        var other = (await db.AccountLogins.SingleAsync(x => x.Subject == "wipe-other")).AccountId;
        var now = DateTimeOffset.UtcNow;

        var mine = Playlist(me, "mine");
        var shared = Playlist(me, "shared", other);
        var theirs = Playlist(other, "theirs", me);
        var system = Playlist(me, "own chords");
        system.SystemKind = PlaylistSystemKind.OwnChords;
        var systemSong = new PlaylistSong { Id = Guid.NewGuid(), Title = "a song", AddedAt = now };
        system.Songs.Add(systemSong);
        db.Playlists.AddRange(mine, shared, theirs, system);
        var chords = new ChordsItem { Id = Guid.NewGuid(), SourceId = $"test:wipe:{Guid.NewGuid():N}", CreatedAt = now };
        db.Chords.Add(chords);
        var mark = new Mark { Id = Guid.NewGuid(), AccountId = me, ChordsId = chords.Id, Kind = "heart_red", CreatedAt = now };
        db.Marks.Add(mark);
        db.ShareLinks.AddRange(
            new ShareLink { Token = Guid.NewGuid().ToString("N"), Kind = ShareTargetKind.Playlist, PlaylistId = mine.Id, CreatedById = me, CreatedAt = now },
            new ShareLink { Token = Guid.NewGuid().ToString("N"), Kind = ShareTargetKind.Playlist, PlaylistId = shared.Id, CreatedById = me, CreatedAt = now });
        db.Jobs.Add(new Job { Id = Guid.NewGuid(), AccountId = me, Kind = "book", Input = "{}", CreatedAt = now });
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await person.SendAsync(HttpMethod.Post, "/api/me/wipe")).StatusCode);

        db.ChangeTracker.Clear();
        // The profile stays, signed in.
        Assert.Equal(HttpStatusCode.OK, (await person.SendAsync(HttpMethod.Get, "/api/me")).StatusCode);
        Assert.Null((await db.Accounts.SingleAsync(x => x.Id == me)).DeletedAt);

        // Marks, own playlists, memberships go; a shared playlist passes on; the system playlist stays, emptied.
        Assert.NotNull((await db.Marks.IgnoreQueryFilters().SingleAsync(x => x.Id == mark.Id)).DeletedAt);
        Assert.NotNull((await db.Playlists.IgnoreQueryFilters().SingleAsync(x => x.Id == mine.Id)).DeletedAt);
        Assert.Equal(other, (await db.Playlists.SingleAsync(x => x.Id == shared.Id)).OwnerId);
        Assert.False(await db.PlaylistMembers.AnyAsync(x => x.PlaylistId == theirs.Id && x.AccountId == me));
        Assert.True(await db.Playlists.AnyAsync(x => x.Id == system.Id));
        Assert.NotNull((await db.PlaylistSongs.IgnoreQueryFilters().SingleAsync(x => x.Id == systemSong.Id)).DeletedAt);

        // The link into the removed playlist and the jobs are deleted; the link into the one passed on stays.
        Assert.False(await db.ShareLinks.AnyAsync(x => x.PlaylistId == mine.Id));
        Assert.True(await db.ShareLinks.AnyAsync(x => x.PlaylistId == shared.Id));
        Assert.False(await db.Jobs.AnyAsync(x => x.AccountId == me));
    }

    [DatabaseFact]
    public async Task APersonIsKnownToTheSourcesByTheirAccountTheAccountsMergedIntoItAndTheirTelegramSignIns()
    {
        var now = DateTimeOffset.UtcNow;
        var (me, merged, earlier) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using (var db = database.CreateContext())
        {
            db.Accounts.Add(new() { Id = me, DisplayName = "me", CreatedAt = now });
            db.AccountLogins.Add(new() { Id = Guid.NewGuid(), AccountId = me, Provider = "telegram", Subject = "4242", CreatedAt = now, LastSignInAt = now });
            db.AccountLogins.Add(new() { Id = Guid.NewGuid(), AccountId = me, Provider = "dev", Subject = $"dev-{me:N}", CreatedAt = now, LastSignInAt = now });
            db.AccountMerges.Add(new() { MergedId = earlier, IntoId = me, MergedAt = now.AddDays(-1) });
            db.AccountMerges.Add(new() { MergedId = merged, IntoId = me, MergedAt = now });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
            Assert.Equal([$"account:{me:D}", $"account:{earlier:D}", $"account:{merged:D}", "telegram:4242"], await AccountSubjects.LoadAsync(db, me, CancellationToken.None));
    }
    internal static Playlist Playlist(Guid ownerId, string title, params Guid[] members)
    {
        var now = DateTimeOffset.UtcNow;
        var playlist = new Playlist { Id = Guid.NewGuid(), OwnerId = ownerId, Title = title, CreatedAt = now, UpdatedAt = now };
        playlist.Members.Add(new() { AccountId = ownerId, Role = PlaylistRole.Owner, JoinedAt = now });
        playlist.Members.AddRange(members.Select((x, i) => new PlaylistMember { AccountId = x, Role = PlaylistRole.Member, JoinedAt = now.AddSeconds(i + 1) }));
        return playlist;
    }
}
