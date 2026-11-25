using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Import;
using HarmonyDB.Shelf.Import.Old;
using HarmonyDB.Shelf.Import.Planning;
using HarmonyDB.Shelf.Model.Marks;
using static HarmonyDB.Shelf.Tests.OldShelfImport.OldShelfBuilder;

namespace HarmonyDB.Shelf.Tests.OldShelfImport;

/// <summary>
/// The import's mapping of the old shelf, on made-up data.
/// </summary>
public class PlannerTests
{
    private const long Bob = 201;
    private const int BobsTenant = 2;

    [Fact]
    public void PersonalPlaylistHoldsOnlyWhatGoesBeyondTheTemplates()
    {
        var old = new OldShelfBuilder();
        old.Template(5, "template song", "t5");
        old.Template(6, "touched template", "t6");
        old.Tenant(BobsTenant, 1000).User(Bob, BobsTenant);
        old.Song(BobsTenant, 5, "template song", "t5");               // a copy never touched
        var touched = old.Song(BobsTenant, 6, "touched template", "t6"); // a copy he added chords to
        old.Version(touched, "bob-extra", Bob);
        old.Song(BobsTenant, 1001, "his own", "own-1");

        var plan = old.Plan();

        var personal = plan.Playlists.Single(x => x.Key == Planner.PersonalKey(Bob));
        Assert.Equal(PlaylistAccess.Private, personal.Access);
        Assert.Equal(new[] { "id:bob-extra", "id:own-1" }, personal.Songs.SelectMany(x => x.Items).Select(x => x.SourceId).Order());
        Assert.Equal(new[] { "id:t5", "id:t6" }, plan.Playlists.Single(x => x.Key == Planner.TemplatesKey).Songs.SelectMany(x => x.Items).Select(x => x.SourceId).Order());
    }

    [Fact]
    public void ATenantWhoseCopyingFailedHasOnlyItsOwnSongs()
    {
        var old = new OldShelfBuilder();
        old.Template(1, "template", "t1");
        old.Tenant(BobsTenant, 3).User(Bob, BobsTenant);
        old.Song(BobsTenant, 1, "first own", "own-1");
        old.Song(BobsTenant, 2, "second own", "t1"); // his own song, at a template's chords

        var personal = old.Plan().Playlists.Single(x => x.Key == Planner.PersonalKey(Bob));

        // his own songs are his, numbered from 1; chords of the template playlist are not repeated in his
        Assert.Equal(new[] { "id:own-1" }, personal.Songs.SelectMany(x => x.Items).Select(x => x.SourceId));
    }

    [Fact]
    public void LikesBecomeHeartsOnTheChordsTheSongOpenedAt()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "two versions", "older", "newer");
        var other = old.Song(SharedTenant, 1002, "liked version", "a", "b");
        old.Like(Alice, other, null, 0, list: null); // ignored: no level, no version
        old.Like(Alice, other, old.VersionOf(other, "b"), 1);
        old.Like(Alice, song, old.VersionOf(song, "newer"), 2);
        old.Like(Alice, song, null, 3);   // a like of the song: on the version it opened at — the one she liked
        old.Like(Alice, other, null, 2);  // opens at "b" (liked), and b already has 1: the highest stays

        var plan = old.Plan();

        var hearts = plan.Marks.Where(x => x.TelegramId == Alice).ToDictionary(x => x.SourceId, x => x.Kind);
        Assert.Equal(MarkKinds.OldLikeLevels[3].Key, hearts["id:newer"]);
        Assert.Equal(MarkKinds.OldLikeLevels[2].Key, hearts["id:b"]);
        Assert.Equal(2, hearts.Count);
        Assert.Contains(plan.Unmapped, x => x.Reason == UnmappedReasons.NoVersionNoLevel && x.TelegramId == Alice);
    }

    [Fact]
    public void TheDefaultVersionPutsOwnChordsFirst()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "song", "plain");
        old.Version(song, "others-own", Keeper, collectiveType: OldCollectiveType.Public, collectiveId: Guid.NewGuid());
        old.Version(song, "her-own", Alice, collectiveType: OldCollectiveType.Public, collectiveId: Guid.NewGuid());
        old.Like(Alice, song, null, 1);
        old.Like(Keeper, song, null, 1);

        var plan = old.Plan();

        Assert.Equal("id:her-own", plan.Marks.Single(x => x.TelegramId == Alice).SourceId);
        Assert.Equal("id:others-own", plan.Marks.Single(x => x.TelegramId == Keeper).SourceId);
    }

    [Fact]
    public void KeysAreWhatThePersonSawEverywhereAndHereInTheirLists()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "song", "a", "b", "c");
        var list = old.List(Alice, SharedTenant, "campfire", OldListAccess.Private);
        old.Like(Alice, song, old.VersionOf(song, "a"), 0, transpose: 2);           // kept with a key: a key and a white heart
        old.Like(Alice, song, old.VersionOf(song, "a"), 0, transpose: 5, list: list); // in the list: "here"; "mine" stays 2
        old.Like(Alice, song, old.VersionOf(song, "b"), 0, transpose: -3, list: list); // only in a list: "here" and "mine"
        old.Like(Alice, song, old.VersionOf(song, "c"), 1, transpose: 0);              // a heart with no key

        var plan = old.Plan();

        var keys = plan.Keys.Where(x => x.TelegramId == Alice).Select(x => (x.SourceId, x.PlaylistKey, x.Semitones)).ToHashSet();
        Assert.Equal(
            new HashSet<(string, string?, int)>
            {
                ("id:a", null, 2),
                ("id:b", null, -3),
                ("id:a", Planner.ListKey(list), 5),
                ("id:b", Planner.ListKey(list), -3),
            },
            keys);
        Assert.Equal(
            new[] { ("id:a", MarkKinds.OldLikeLevels[0].Key), ("id:c", MarkKinds.OldLikeLevels[1].Key) },
            plan.Marks.Where(x => x.TelegramId == Alice).Select(x => (x.SourceId, x.Kind)));
        Assert.Contains(plan.Remarks, x => x.Reason == UnmappedReasons.HereDiffersFromMine && x.What.Contains("id:a"));
    }

    [Fact]
    public void ListsKeepTheirAccess()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "song", "a", "b");
        var view = old.List(Alice, SharedTenant, "for viewing", OldListAccess.SharedView);
        var edit = old.List(Alice, SharedTenant, "for editing", OldListAccess.SharedEditSingle);
        var mine = old.List(Alice, SharedTenant, "private", OldListAccess.Private);
        old.Like(Alice, song, null, 0, list: view);                          // the song: all its versions
        old.Like(Keeper, song, old.VersionOf(song, "b"), 0, list: edit);  // a version, added by another member
        old.Like(Alice, song, old.VersionOf(song, "a"), 0, list: mine);

        var playlists = old.Plan().Playlists.ToDictionary(x => x.Key);

        var viewing = playlists[Planner.ListKey(view)];
        Assert.Equal((PlaylistAccess.Private, PlaylistAddPolicy.Admins, Alice), (viewing.Access, viewing.AddPolicy, viewing.OwnerTelegramId));
        Assert.Equal(new[] { Keeper }, viewing.Members.Select(x => x.TelegramId));
        Assert.Equal(new[] { "id:a", "id:b" }, viewing.Songs.Single().Items.Select(x => x.SourceId));

        var editing = playlists[Planner.ListKey(edit)];
        Assert.Equal(PlaylistAddPolicy.AllMembers, editing.AddPolicy);
        Assert.Equal(Keeper, editing.Songs.Single().Items.Single().AddedBy);

        Assert.Empty(playlists[Planner.ListKey(mine)].Members);
    }

    [Fact]
    public void TheSharedTenantIsWholeAndTheTemplatesArePublicWithTheirRecommendedKeys()
    {
        var old = new OldShelfBuilder();
        old.Template(1, "template", "t1", published: "transpose=-2, two-columns");
        old.Template(2, "template two", "t2", published: "transpose=0");
        old.Song(SharedTenant, 1001, "beyond", "x");
        old.Song(SharedTenant, 1002, "archived", OldSongStatus.Archived, Alice, "y");

        var plan = old.Plan();

        var shared = plan.Playlists.Single(x => x.Key == Planner.TenantKey(SharedTenant));
        Assert.Equal(new[] { "id:t1", "id:t2", "id:x" }, shared.Songs.SelectMany(x => x.Items).Select(x => x.SourceId));
        Assert.Equal(new[] { Alice }, shared.Members.Select(x => x.TelegramId));
        var templates = plan.Playlists.Single(x => x.Key == Planner.TemplatesKey);
        Assert.Equal((PlaylistAccess.Public, Keeper), (templates.Access, templates.OwnerTelegramId));
        Assert.Equal(new[] { ("id:t1", -2) }, plan.Keys.Where(x => x.PlaylistKey == Planner.TemplatesKey && x.TelegramId == Keeper).Select(x => (x.SourceId, x.Semitones)));
        Assert.Contains("custom-key", plan.People.Single(x => x.TelegramId == Alice).Permissions);
    }

    [Fact]
    public void CommentsBecomeNotesWhereTheSongIs()
    {
        var old = new OldShelfBuilder();
        old.Template(5, "template", "t5");
        var shared = old.Song(SharedTenant, 1001, "song", "a", "b");
        old.Comment(Alice, shared, null, "on the song");
        old.Comment(Alice, shared, old.VersionOf(shared, "b"), "on b");
        old.Tenant(BobsTenant, 1000).User(Bob, BobsTenant);
        var copy = old.Song(BobsTenant, 5, "template", "t5");
        old.Comment(Bob, copy, old.VersionOf(copy, "t5"), "on a template");

        var notes = old.Plan().Notes;

        var alice = notes.Single(x => x.TelegramId == Alice);
        Assert.Equal((Planner.TenantKey(SharedTenant), Planner.SongKey(shared)), (alice.PlaylistKey, alice.SongKey));
        Assert.Equal(new[] { ("on the song", (string?)null), ("on b", "id:b") }, alice.Parts.Select(x => (x.Text, x.SourceId)));
        var bob = notes.Single(x => x.TelegramId == Bob);
        Assert.Equal(("id:t5", "on a template"), (bob.SourceId, bob.Parts.Single().Text));
    }

    [Fact]
    public void WhatIsNotResolvedWaitsAndNothingIsLostSilently()
    {
        var old = new OldShelfBuilder().Unresolved("lost").Unresolved("half");
        var song = old.Song(SharedTenant, 1001, "only unresolved", "lost");
        var half = old.Song(SharedTenant, 1002, "half", "half", "ok");
        old.Like(Alice, song, old.VersionOf(song, "lost"), 3);
        old.Like(Alice, half, old.VersionOf(half, "half"), 2, transpose: 0);

        var plan = old.Plan();

        var shared = plan.Playlists.Single(x => x.Key == Planner.TenantKey(SharedTenant));
        Assert.Equal(new[] { "id:ok" }, shared.Songs.SelectMany(x => x.Items).Select(x => x.SourceId));
        Assert.Empty(plan.Marks);
        Assert.Equal(2, plan.Unmapped.Count(x => x.Kind == "variant" && x.Pending));
        Assert.Equal(2, plan.Unmapped.Count(x => x.Kind == "like" && x.Pending));
    }

    [Fact]
    public void ScopeLeavesOutUnconfiguredSharedTenantsAndGivesLeftTenantsToTheirPerson()
    {
        var old = new OldShelfBuilder();
        old.Tenant(7).User(701, 7).User(702, 7);
        old.Song(7, 1001, "theirs", "z");
        old.Tenant(8, 1000, $"Personal {Alice}");
        old.Song(8, 1001, "left behind", "w");
        old.User(-5, BobsTenant).Tenant(BobsTenant);

        var plan = old.Plan();

        Assert.DoesNotContain(plan.People, x => x.TelegramId is 701 or 702 or -5);
        Assert.Contains(plan.Unmapped, x => x.Reason == UnmappedReasons.TenantNotConfigured);
        Assert.Equal(new[] { "id:w" }, plan.Playlists.Single(x => x.Key == Planner.PersonalKey(Alice)).Songs.SelectMany(x => x.Items).Select(x => x.SourceId));

    }

    [Fact]
    public void ATenantNotImportedGivesOnlyItsNamedPeopleWithWhatTheyAdded()
    {
        var old = new OldShelfBuilder();
        old.Song(SharedTenant, 1001, "original", "x");
        old.Tenant(7).User(701, 7).User(702, 7);
        old.Song(7, 1001, "copy", OldSongStatus.Live, 701, "x");             // his, but the first tenant has it
        var his = old.Song(7, 1002, "Ð¿ÐµÑ\u0081Ð½Ñ\u008f", OldSongStatus.Live, 701, "his");  // his own, its title mis-decoded
        var theirs = old.Song(7, 1003, "theirs", OldSongStatus.Live, 702, "theirs");
        old.Like(701, his, old.VersionOf(his, "his"), 3, transpose: 2);
        old.Like(701, theirs, old.VersionOf(theirs, "theirs"), 3);
        old.Like(702, theirs, old.VersionOf(theirs, "theirs"), 3);
        old.Visit(701, "elsewhere");
        var options = Options();
        options.NotImportedTenants = [new() { TenantId = 7, ImportPeople = [701], BeyondTenantId = SharedTenant }];

        var plan = old.Plan(options);

        Assert.Contains(plan.People, x => x.TelegramId == 701);
        Assert.DoesNotContain(plan.People, x => x.TelegramId == 702);
        Assert.Equal(new[] { "id:his" }, plan.Playlists.Single(x => x.Key == Planner.PersonalKey(701)).Songs.SelectMany(x => x.Items).Select(x => x.SourceId));
        Assert.Equal(new[] { "id:his" }, plan.Marks.Select(x => x.SourceId));
        Assert.Equal(new[] { "id:his" }, plan.Keys.Select(x => x.SourceId));
        Assert.Equal(new[] { "id:elsewhere" }, plan.Recent.Select(x => x.SourceId));
        Assert.DoesNotContain(plan.Playlists, x => x.Key == Planner.TenantKey(7));
        Assert.Contains(plan.Unmapped, x => x.Reason == UnmappedReasons.TenantNotImported);
        Assert.Contains(plan.Remarks, x => x.What.Contains($"song {his}"));
    }
}