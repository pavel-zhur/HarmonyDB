using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Auth;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Share;
using HarmonyDB.Shelf.Model.Sync;
using HarmonyDB.Shelf.Tests.Auth;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Playlists;

/// <summary>
/// The rules of playlists, songs, marks, keys, notes, links, undo and the sync, walked through the API as the browser
/// does.
/// </summary>
public sealed class PlaylistsTests(LibraryFixture fixture) : IClassFixture<LibraryFixture>
{
    [DatabaseFact]
    public async Task LettersLinkingAndUnlinkingKeepEveryonesChoice()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        await JoinAsync(ann, bob, playlist, PlaylistRole.Member);
        string c1 = Id(), c2 = Id(), c3 = Id(), c4 = Id();

        var a = await AddAsync(ann, playlist, c1, "Song A");
        var b = await AddAsync(ann, playlist, c2, "Song B");
        var c = await AddAsync(ann, playlist, c3, "Song C");
        await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs/{a}/variants", new AddVariantRequest { Chords = c4 });
        Assert.Equal([c1, c4], (await PageAsync(bob, playlist, a)).Variants.Select(x => x.Chords.Id));

        // the same chords are not put in one playlist twice
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = c2, Title = "again" }), HttpStatusCode.Conflict, PlaylistErrorCodes.AlreadyInPlaylist);

        // Bob's main variant of B goes with it into A
        var bItem = (await PageAsync(bob, playlist, b)).Variants.Single().ItemId;
        await Ok<UndoOnly>(bob, HttpMethod.Put, $"/api/playlists/{playlist}/songs/{b}/main", new SetMainVariantRequest { ItemId = bItem });
        await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs/{a}/link", new LinkSongsRequest { SongIds = [b, c] });

        var linked = await PageAsync(bob, playlist, a);
        Assert.Equal([c1, c4, c2, c3], linked.Variants.Select(x => x.Chords.Id));
        Assert.Equal([0, 1, 2, 3], linked.Variants.Select(x => x.Index));
        Assert.Equal(linked.Variants[2].ItemId, linked.MyMainItemId);
        Assert.Null((await PageAsync(ann, playlist, a)).MyMainItemId);
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs/{b}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchSong);

        // removing a variant shifts the letters after it
        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{a}/variants/{linked.Variants[1].ItemId}");
        var shifted = await PageAsync(bob, playlist, a);
        Assert.Equal([c1, c2, c3], shifted.Variants.Select(x => x.Chords.Id));
        Assert.Equal(1, shifted.Variants.Single(x => x.Chords.Id == c2).Index);

        // unlinking: the variant becomes a song of its own, Bob's choice with it; undone, all is as it was
        var unlinked = await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs/{a}/variants/{shifted.Variants[1].ItemId}/unlink");
        var own = await PageAsync(bob, playlist, unlinked.Value.Id);
        Assert.Equal([c2], own.Variants.Select(x => x.Chords.Id));
        Assert.Equal(own.Variants[0].ItemId, own.MyMainItemId);
        Assert.Null((await PageAsync(bob, playlist, a)).MyMainItemId);

        await UndoAsync(ann, unlinked.Undo);
        var back = await PageAsync(bob, playlist, a);
        Assert.Equal([c1, c2, c3], back.Variants.Select(x => x.Chords.Id));
        Assert.Equal(shifted.Variants[1].ItemId, back.MyMainItemId);
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs/{unlinked.Value.Id}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchSong);
    }

    [DatabaseFact]
    public async Task OthersMarksAndKeysAreSeenOnlyWhereBothAreMembers()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var (carol, carolId) = await fixture.PersonAsync("carol");
        var playlist = await CreateAsync(ann, PlaylistAccess.Public);
        await JoinAsync(ann, bob, playlist, PlaylistRole.Member);
        var chords = Id();
        var song = await AddAsync(ann, playlist, chords, "Song");

        var results = await SyncAsync(ann, null,
            new() { Type = ChangeType.Mark, Chords = chords, Kind = "heart_red", On = true },
            new() { Type = ChangeType.Key, Chords = chords, Semitones = 2 },
            new() { Type = ChangeType.Key, Chords = chords, PlaylistId = playlist, Semitones = 2 },
            new() { Type = ChangeType.Note, SongId = song, Text = "capo 2" });
        Assert.All(results.Results, x => Assert.True(x.Applied));
        await SyncAsync(carol, null, new Change { Type = ChangeType.Mark, Chords = chords, Kind = "star", On = true });

        // Bob, a member: Ann's mark, both her keys (kept even when equal), her note
        var bobs = await PageAsync(bob, playlist, song);
        Assert.Equal(["heart_red"], bobs.Marks.Where(x => x.AccountId == annId).Select(x => x.Kind));
        Assert.Equal([(2, false), (2, true)], bobs.Keys.Where(x => x.AccountId == annId).Select(x => (x.Semitones, x.Here)).OrderBy(x => x.Here));
        Assert.Equal(["capo 2"], bobs.Notes.Select(x => x.Text));
        Assert.DoesNotContain(bobs.Marks, x => x.AccountId == carolId);

        // Carol only looks at the public playlist: her own mark; of the members', only the playlist's content — the
        // "here" key and the note
        var carols = await PageAsync(carol, playlist, song);
        Assert.Equal([carolId], carols.Marks.Select(x => x.AccountId));
        Assert.Equal([(annId, 2, true)], carols.Keys.Select(x => (x.AccountId, x.Semitones, x.Here)));
        Assert.Equal(["capo 2"], carols.Notes.Select(x => x.Text));

        // a guest sees no marks
        var guests = await PageAsync(fixture.Api.NewBrowser(), playlist, song);
        Assert.Empty(guests.Marks);

        // the lists: Bob sees Ann's mark as another's; Carol sees no one's but her own
        var bobsRow = (await Ok<SongList>(bob, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.Single();
        Assert.Equal(1, bobsRow.OtherPeople);
        Assert.Equal(["heart_red"], bobsRow.OtherMarks);
        var carolsRow = (await Ok<SongList>(carol, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.Single();
        Assert.Equal(0, carolsRow.OtherPeople);
        Assert.Equal(["star"], carolsRow.MyMarks);

        // a "here" key is for members, on chords of the playlist
        var refused = await SyncAsync(carol, null, new Change { Type = ChangeType.Key, Chords = chords, PlaylistId = playlist, Semitones = 1 });
        Assert.Equal(PlaylistErrorCodes.MembersOnly, refused.Results.Single().Code);
        var elsewhere = await SyncAsync(bob, null, new Change { Type = ChangeType.Key, Chords = Id(), PlaylistId = playlist, Semitones = 1 });
        Assert.Equal(PlaylistErrorCodes.ChordsNotInPlaylist, elsewhere.Results.Single().Code);
    }

    [DatabaseFact]
    public async Task WhoMayDoWhatAndWhoSeesWhat()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, bobId) = await fixture.PersonAsync("bob");
        var (carol, _) = await fixture.PersonAsync("carol");
        var guest = fixture.Api.NewBrowser();
        var playlist = await CreateAsync(ann, PlaylistAccess.Private, PlaylistAddPolicy.Admins);
        await JoinAsync(ann, bob, playlist, PlaylistRole.Member);
        var anns = await AddAsync(ann, playlist, Id(), "Ann's");

        // adding is for admins here; removing others' things too
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = Id(), Title = "x" }), HttpStatusCode.Forbidden, PlaylistErrorCodes.AddingNotAllowed);
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { AddPolicy = PlaylistAddPolicy.AllMembers });
        var bobs = await AddAsync(bob, playlist, Id(), "Bob's");
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{anns}"), HttpStatusCode.Forbidden, PlaylistErrorCodes.NotYours);
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Title = "mine" }), HttpStatusCode.Forbidden, PlaylistErrorCodes.AdminsOnly);
        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{bobs}");

        // a private playlist is not there for others; a guest's change asks to sign in
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchPlaylist);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchPlaylist);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Post, "/api/sync", new SyncRequest { AccountId = Guid.NewGuid() }), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);

        // a song's link from a private playlist: the song and its chords, not the playlist
        var link = await Ok<ShareLinkItem>(bob, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = anns });
        Assert.Equal(link.Token, (await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = anns })).Token);
        var opened = await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{link.Token}");
        Assert.True(opened.Song!.FromPrivatePlaylist);
        Assert.Null(opened.Song.Playlist);
        Assert.Null(opened.Song.Song.AddedBy);
        Assert.Single(opened.Song.Variants);
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs?share={link.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchPlaylist);

        // what one sees by a link may be put in one's own playlist
        var carols = await CreateAsync(carol, PlaylistAccess.Private);
        await Ok<Changed<SongRef>>(carol, HttpMethod.Post, $"/api/playlists/{carols}/songs", new AddSongRequest { FromSongId = anns, Share = link.Token });
        Assert.Equal(["Ann's"], (await Ok<SongList>(carol, HttpMethod.Get, $"/api/playlists/{carols}/songs")).Rows.Select(x => x.Title));

        // of a "link" playlist: the playlist too, by the song's link or its own
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Access = PlaylistAccess.Link });
        var linked = await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{link.Token}");
        Assert.False(linked.Song!.FromPrivatePlaylist);
        Assert.Equal(playlist, linked.Song.Playlist!.Id);
        var own = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Playlist, PlaylistId = playlist });
        var members = (await Ok<PlaylistDetails>(guest, HttpMethod.Get, $"/api/playlists/{playlist}?share={own.Token}")).Members;
        Assert.Contains(members, x => x.Person.Id == bobId);
        Assert.Single((await Ok<SongList>(guest, HttpMethod.Get, $"/api/playlists/{playlist}/songs?share={own.Token}")).Rows);

        // made private again, the playlist's link closes; the song's link still shows the song alone
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Access = PlaylistAccess.Private });
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/share/{own.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);
        Assert.True((await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{link.Token}")).Song!.FromPrivatePlaylist);
    }

    [DatabaseFact]
    public async Task InvitationsAreTheWayIn()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, bobId) = await fixture.PersonAsync("bob");
        var (carol, _) = await fixture.PersonAsync("carol");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        var once = (await Ok<Changed<InvitationItem>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/invitations", new CreateInvitationRequest { Role = PlaylistRole.Admin, MaxUses = 1 })).Value;

        Assert.Equal(playlist, (await Ok<InvitationView>(fixture.Api.NewBrowser(), HttpMethod.Get, $"/api/playlists/invitations/{once.Token}")).PlaylistId);
        await Browser.AssertError(await fixture.Api.NewBrowser().SendAsync(HttpMethod.Post, $"/api/playlists/invitations/{once.Token}/accept"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        var joined = await Ok<Changed<PlaylistDetails>>(carol, HttpMethod.Post, $"/api/playlists/invitations/{once.Token}/accept");
        Assert.Equal(PlaylistRole.Admin, joined.Value.Playlist.MyRole);

        // used up, it tells nothing of the playlist; Carol's joining undone gives its use back, and Bob takes it
        await Browser.AssertError(await fixture.Api.NewBrowser().SendAsync(HttpMethod.Get, $"/api/playlists/invitations/{once.Token}"), HttpStatusCode.Gone, PlaylistErrorCodes.InvitationInvalid);
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Post, $"/api/playlists/invitations/{once.Token}/accept"), HttpStatusCode.Gone, PlaylistErrorCodes.InvitationInvalid);
        await UndoAsync(carol, joined.Undo);
        await Ok<Changed<PlaylistDetails>>(bob, HttpMethod.Post, $"/api/playlists/invitations/{once.Token}/accept");
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Post, $"/api/playlists/invitations/{once.Token}/accept"), HttpStatusCode.Gone, PlaylistErrorCodes.InvitationInvalid);

        var revoked = (await Ok<Changed<InvitationItem>>(bob, HttpMethod.Post, $"/api/playlists/{playlist}/invitations", new CreateInvitationRequest())).Value;
        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/invitations/{revoked.Token}");
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Get, $"/api/playlists/invitations/{revoked.Token}"), HttpStatusCode.Gone, PlaylistErrorCodes.InvitationInvalid);
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Post, $"/api/playlists/invitations/{revoked.Token}/accept"), HttpStatusCode.Gone, PlaylistErrorCodes.InvitationInvalid);

        // the owner passes the playlist on before leaving; an admin does not remove another admin
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/leave"), HttpStatusCode.Conflict, PlaylistErrorCodes.OwnerCannotLeave);
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/owner", new PassOwnershipRequest { AccountId = bobId });
        await Ok<UndoOnly>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/leave");
        var after = await Ok<PlaylistDetails>(bob, HttpMethod.Get, $"/api/playlists/{playlist}");
        Assert.Equal(bobId, after.Playlist.Owner.Id);
        Assert.Single(after.Members);
    }

    [DatabaseFact]
    public async Task RemovalsAreTombstonesAndUndoBringsTheRowBack()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        var chords = Id();
        await AddAsync(ann, playlist, chords, "Song");

        // a playlist deleted is marked, and comes back by its undo
        var deleted = await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}");
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchPlaylist);
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Post, "/api/sync/undo", new UndoRequest { Token = deleted.Undo! }), HttpStatusCode.Gone, PlaylistErrorCodes.UndoExpired);
        await UndoAsync(ann, deleted.Undo);
        Assert.Single((await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows);

        // a mark taken off comes back as the same row; when a new one has taken its place there is nothing to undo
        await SyncAsync(ann, null, new Change { Type = ChangeType.Mark, Chords = chords, Kind = "fire", On = true });
        await using var db = fixture.CreateContext();
        var markId = await db.Marks.Where(x => x.AccountId == annId && x.Chords.SourceId == chords).Select(x => x.Id).SingleAsync();
        var off = (await SyncAsync(ann, null, new Change { Type = ChangeType.Mark, Chords = chords, Kind = "fire", On = false })).Results.Single().Undo!;
        await UndoAsync(ann, off);
        Assert.Equal(markId, await db.Marks.Where(x => x.AccountId == annId && x.Chords.SourceId == chords).Select(x => x.Id).SingleAsync());

        var offAgain = (await SyncAsync(ann, null, new Change { Type = ChangeType.Mark, Chords = chords, Kind = "fire", On = false })).Results.Single().Undo!;
        await SyncAsync(ann, null, new Change { Type = ChangeType.Mark, Chords = chords, Kind = "fire", On = true });
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, "/api/sync/undo", new UndoRequest { Token = offAgain }), HttpStatusCode.Conflict, PlaylistErrorCodes.NothingToUndo);

        // what is mine in the playlist goes, and comes back by one undo; marks are not its business
        var song = (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.Single().SongId!.Value;
        await SyncAsync(ann, null,
            new Change { Type = ChangeType.Key, Chords = chords, PlaylistId = playlist, Semitones = 3 },
            new Change { Type = ChangeType.Note, SongId = song, Text = "mine here" });
        var cleared = await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/mine");
        var empty = await PageAsync(ann, playlist, song);
        Assert.Empty(empty.Notes);
        Assert.DoesNotContain(empty.Keys, x => x.Here);
        Assert.Single(empty.Marks);
        await UndoAsync(ann, cleared.Undo);
        var back = await PageAsync(ann, playlist, song);
        Assert.Equal(["mine here"], back.Notes.Select(x => x.Text));
        Assert.Contains(back.Keys, x => x is { Here: true, Semitones: 3 });
    }

    [DatabaseFact]
    public async Task TheSyncTakesABatchAndGivesWhatChanged()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        await JoinAsync(ann, bob, playlist, PlaylistRole.Member);
        string one = Id(), two = Id();
        await AddAsync(ann, playlist, one, "One");

        var first = await SyncAsync(ann, null,
            new() { Type = ChangeType.Mark, Chords = one, Kind = "heart_red", On = true },
            new() { Type = ChangeType.Mark, Chords = two, Kind = "no_such_kind", On = true },
            new() { Type = ChangeType.Note, Chords = two, Text = new string('x', 10001) },
            new() { Type = ChangeType.Note, Chords = two, Text = "mine" },
            new() { Type = ChangeType.Opened, Chords = one });
        Assert.Equal([true, false, false, true, true], first.Results.Select(x => x.Applied));
        Assert.Equal([null, PlaylistErrorCodes.UnknownMarkKind, PlaylistErrorCodes.TooLong, null, null], first.Results.Select(x => x.Code));
        Assert.Equal([one], first.Marks.Select(x => x.Chords));
        Assert.Equal(["mine"], first.Notes.Select(x => x.Text));

        // since the watermark: what changed, a removal as such. The watermark is the oldest transaction still running
        // anywhere on the server (another test's too), so a row written before it may come again: never missed, never
        // wrong — what comes is the thing as it is now
        var second = await SyncAsync(ann, first.Watermark, new Change { Type = ChangeType.Mark, Chords = one, Kind = "heart_red", On = false });
        Assert.Contains(second.Marks, x => (x.Chords, x.Kind, x.On) == (one, "heart_red", false));
        Assert.All(second.Notes, x => Assert.Equal((two, "mine"), (x.Chords, x.Text)));
        await SyncAsync(bob, null, new Change { Type = ChangeType.Mark, Chords = one, Kind = "star", On = true });
        var third = await SyncAsync(ann, second.Watermark);
        Assert.All(third.Marks, x => Assert.False(x.On));
        Assert.Contains(playlist, third.Playlists);

        // the library: the playlist's song, and the chords only noted
        var library = await Ok<SongList>(ann, HttpMethod.Get, "/api/library?limit=1000");
        Assert.Contains(library.Rows, x => x.Title == "One" && x.PlaylistId == playlist);
        Assert.Contains(library.Rows, x => x.Chords?.Id == two);
        Assert.Contains(library.Rows, x => x.Title == "One" && x.OtherMarks.SequenceEqual(["star"]));
    }

    [DatabaseFact]
    public async Task AnImportedPlaylistSyncsByItsRules()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        fixture.Audio.Tracks.AddRange([("t1", "First", "X"), ("t2", "Second", "X"), ("t3", "Third", "Y")]);
        var imported = await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Post, "/api/playlists/import", new ImportPlaylistRequest { Url = "https://music.youtube.com/playlist?list=PL1" });
        var playlist = imported.Value.Playlist.Id;
        await WaitForSyncAsync(ann, playlist);
        var details = await Ok<PlaylistDetails>(ann, HttpMethod.Get, $"/api/playlists/{playlist}");
        Assert.Equal("A playlist there", details.Playlist.Title);
        Assert.True(details.Playlist.Origin!.CanSync);
        var songs = (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows;
        Assert.Equal(["First", "Second", "Third"], songs.Select(x => x.Title));
        Assert.All(songs, x => Assert.Equal(1, x.RecordingsCount));

        // edits here: one renamed, one removed; there: one removed, one added
        await Ok<Changed<SongRef>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}/songs/{songs[0].SongId}", new UpdateSongRequest { Title = "Mine now" });
        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{songs[2].SongId}");
        fixture.Audio.Tracks.RemoveAt(1);
        fixture.Audio.Tracks.Add(("t4", "Fourth", "Z"));
        fixture.Audio.Title = "Renamed there";

        // only the importer syncs
        var invitation = (await Ok<Changed<InvitationItem>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/invitations", new CreateInvitationRequest { Role = PlaylistRole.Admin })).Value;
        await Ok<Changed<PlaylistDetails>>(bob, HttpMethod.Post, $"/api/playlists/invitations/{invitation.Token}/accept");
        Assert.Null((await Ok<PlaylistDetails>(bob, HttpMethod.Get, $"/api/playlists/{playlist}")).Playlist.Origin);
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/sync"), HttpStatusCode.Forbidden, PlaylistErrorCodes.NotImportedByYou);

        await Ok<PlaylistDetails>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/sync");
        await WaitForSyncAsync(ann, playlist);
        var after = await Ok<PlaylistDetails>(ann, HttpMethod.Get, $"/api/playlists/{playlist}");
        Assert.Equal("A playlist there", after.Playlist.Title);
        Assert.Equal(1, after.Sync!.Result!.Value.GetProperty("added").GetInt32());
        Assert.Equal(["Mine now", "Second", "Fourth"], (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.Select(x => x.Title));
    }

    [DatabaseFact]
    public async Task AStaleQueuedChangeDoesNotOverwriteANewerOne()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var chords = Id();
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);

        await SyncAsync(ann, null,
            new Change { Type = ChangeType.Note, Chords = chords, Text = "written now" },
            new Change { Type = ChangeType.Key, Chords = chords, Semitones = 2 },
            new Change { Type = ChangeType.Mark, Chords = chords, Kind = "star", On = true });
        await SyncAsync(ann, null, new Change { Type = ChangeType.Mark, Chords = chords, Kind = "star", On = false });

        var stale = await SyncAsync(ann, null,
            new Change { Type = ChangeType.Note, Chords = chords, Text = "queued yesterday", At = yesterday },
            new Change { Type = ChangeType.Key, Chords = chords, Semitones = 5, At = yesterday },
            new Change { Type = ChangeType.Mark, Chords = chords, Kind = "star", On = true, At = yesterday },
            new Change { Type = ChangeType.Note, Chords = chords, At = yesterday });
        Assert.All(stale.Results, x => Assert.Equal((false, PlaylistErrorCodes.Stale), (x.Applied, x.Code)));
        Assert.Equal(["written now"], stale.Notes.Select(x => x.Text));
        Assert.Equal([2], stale.Keys.Select(x => x.Semitones));
        Assert.Empty(stale.Marks);

        // a change made later than the last one is applied
        var later = await SyncAsync(ann, null, new Change { Type = ChangeType.Note, Chords = chords, Text = "later", At = DateTimeOffset.UtcNow });
        Assert.True(later.Results.Single().Applied);
        Assert.Equal(["later"], later.Notes.Select(x => x.Text));
    }

    [DatabaseFact]
    public async Task LeavingTakesTheLeaversKeysAndNotesHere()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, bobId) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        await JoinAsync(ann, bob, playlist, PlaylistRole.Member);
        var chords = Id();
        var song = await AddAsync(ann, playlist, chords, "Song");
        await SyncAsync(bob, null,
            new Change { Type = ChangeType.Key, Chords = chords, PlaylistId = playlist, Semitones = 4 },
            new Change { Type = ChangeType.Note, SongId = song, Text = "by Bob" });
        Assert.Equal(["by Bob"], (await PageAsync(ann, playlist, song)).Notes.Select(x => x.Text));

        var left = await Ok<UndoOnly>(bob, HttpMethod.Post, $"/api/playlists/{playlist}/leave");
        var page = await PageAsync(ann, playlist, song);
        Assert.Empty(page.Notes);
        Assert.DoesNotContain(page.Keys, x => x.AccountId == bobId);
        Assert.DoesNotContain((await SyncAsync(bob, null)).Keys, x => x.PlaylistId == playlist);

        await UndoAsync(bob, left.Undo);
        Assert.Equal(["by Bob"], (await PageAsync(ann, playlist, song)).Notes.Select(x => x.Text));

        // the same when an admin removes the member
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/members/{bobId}");
        Assert.Empty((await PageAsync(ann, playlist, song)).Notes);
    }

    [DatabaseFact]
    public async Task APublicPlaylistsKeysHereAndNotesAreSeenByWhoeverSeesIt()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var guest = fixture.Api.NewBrowser();
        var playlist = await CreateAsync(ann, PlaylistAccess.Public);
        var chords = Id();
        var song = await AddAsync(ann, playlist, chords, "Song");
        await SyncAsync(ann, null,
            new Change { Type = ChangeType.Key, Chords = chords, PlaylistId = playlist, Semitones = -2 },
            new Change { Type = ChangeType.Key, Chords = chords, Semitones = 1 },
            new Change { Type = ChangeType.Mark, Chords = chords, Kind = "star", On = true },
            new Change { Type = ChangeType.Note, SongId = song, Text = "play it slow" });

        // the playlist's content: the "here" key and the note; not the mark, not the "mine" key
        var seen = await PageAsync(guest, playlist, song);
        Assert.Equal([(annId, -2, true)], seen.Keys.Select(x => (x.AccountId, x.Semitones, x.Here)));
        Assert.Equal(["play it slow"], seen.Notes.Select(x => x.Text));
        Assert.Empty(seen.Marks);

        // made private, a song's link shows none of it
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Access = PlaylistAccess.Private });
        var link = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = song });
        var hidden = (await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{link.Token}")).Song!;
        Assert.Empty(hidden.Keys);
        Assert.Empty(hidden.Notes);
    }

    [DatabaseFact]
    public async Task LinkingNamesTheSongAndItsLinkFollows()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var guest = fixture.Api.NewBrowser();
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        var a = await AddAsync(ann, playlist, Id(), "A");
        var b = await AddAsync(ann, playlist, Id(), "B");
        var link = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = b });

        var linked = await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs/{a}/link", new LinkSongsRequest { SongIds = [b], Title = "Merged", Artists = ["Both"] });
        Assert.Equal("Merged", linked.Value.Title);
        Assert.Equal(["Both"], linked.Value.Artists);
        var opened = (await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{link.Token}")).Song!;
        Assert.Equal(a, opened.Song.Id);
        Assert.Equal(2, opened.Variants.Count);

        await UndoAsync(ann, linked.Undo);
        var again = (await Ok<ShareView>(guest, HttpMethod.Get, $"/api/share/{link.Token}")).Song!;
        Assert.Equal((b, "B"), (again.Song.Id, again.Song.Title));
        Assert.Equal("A", (await PageAsync(ann, playlist, a)).Song.Title);
    }

    [DatabaseFact]
    public async Task ChordsAreAskedOfTheirSourceForThePerson()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);

        // a source that does not answer, and chords not open to the person (or not known, the same): refused, no row made
        var away = $"away-{Guid.NewGuid():N}";
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = away, Title = "x" }), HttpStatusCode.ServiceUnavailable, ErrorCodes.Unavailable);
        var closed = $"closed-{Guid.NewGuid():N}";
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = closed, Title = "x" }), HttpStatusCode.Forbidden, PlaylistErrorCodes.ChordsNotOpen);
        var marked = await SyncAsync(ann, null, new Change { Type = ChangeType.Mark, Chords = closed, Kind = "star", On = true });
        Assert.Equal(PlaylistErrorCodes.ChordsNotOpen, marked.Results.Single().Code);
        await using (var db = fixture.CreateContext())
            Assert.False(await db.Chords.AnyAsync(x => x.SourceId == away || x.SourceId == closed));

        // open: named as its source told the person, with the source's label in the list
        var open = Id();
        var named = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = open })).Value;
        Assert.Equal($"Title of {open}", named.Title);
        var song = named.Id;
        Assert.Contains((await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows, x => x.Sources.Any(s => s.Key == "A source"));

        // an item held here already (put in by an import) that is not open to the person: seen as not open, with no
        // header, and markable
        await using (var db = fixture.CreateContext())
        {
            var held = new HarmonyDB.Shelf.Database.Model.ChordsItem { Id = Guid.NewGuid(), SourceId = closed, CreatedAt = DateTimeOffset.UtcNow };
            db.Chords.Add(held);
            db.PlaylistSongItems.Add(new() { Id = Guid.NewGuid(), SongId = song, ChordsId = held.Id, Position = 1, AddedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var variants = (await PageAsync(ann, playlist, song)).Variants;
        Assert.Equal([Model.Chords.ChordsState.Open, Model.Chords.ChordsState.NotOpen], variants.Select(x => x.Chords.State));
        Assert.Null(variants[1].Chords.Header);
        Assert.True((await SyncAsync(ann, null, new Change { Type = ChangeType.Mark, Chords = closed, Kind = "star", On = true })).Results.Single().Applied);

        // a copy takes the variants open to the person; a song with none open is not copied
        var bobs = await CreateAsync(bob, PlaylistAccess.Private);
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Access = PlaylistAccess.Public });
        var copied = (await Ok<Changed<AddedSong>>(bob, HttpMethod.Post, $"/api/playlists/{bobs}/songs", new AddSongRequest { FromSongId = song })).Value;
        Assert.Equal(1, copied.LeftOut);
        Assert.Equal([open], (await PageAsync(bob, bobs, copied.Id)).Variants.Select(x => x.Chords.Id));
        var onlyClosed = (await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs/{song}/variants/{variants[1].ItemId}/unlink")).Value.Id;
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Post, $"/api/playlists/{bobs}/songs", new AddSongRequest { FromSongId = onlyClosed }), HttpStatusCode.Forbidden, PlaylistErrorCodes.ChordsNotOpen);
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Access = PlaylistAccess.Private });

        // a note on a song of a playlist one is not in answers as for a song that is not there
        var other = await SyncAsync(bob, null, new Change { Type = ChangeType.Note, SongId = song, Text = "x" }, new Change { Type = ChangeType.Note, SongId = Guid.NewGuid(), Text = "x" });
        Assert.All(other.Results, x => Assert.Equal(PlaylistErrorCodes.NoSuchSong, x.Code));

        // a system playlist stays private
        var system = (await Ok<List<PlaylistSummary>>(ann, HttpMethod.Get, "/api/playlists")).First(x => x.SystemKind != PlaylistSystemKind.None);
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Patch, $"/api/playlists/{system.Id}", new UpdatePlaylistRequest { Access = PlaylistAccess.Public }), HttpStatusCode.Forbidden, PlaylistErrorCodes.SystemPlaylist);
    }

    [DatabaseFact]
    public async Task ARecordingFoundAutomaticallyIsRemovedByWhoeverMayAdd()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        await JoinAsync(ann, bob, playlist, PlaylistRole.Member);
        var song = await AddAsync(ann, playlist, Id(), "Song");
        var found = Guid.NewGuid();
        var imported = Guid.NewGuid();
        await using (var db = fixture.CreateContext())
        {
            var recording = new HarmonyDB.Shelf.Database.Model.Recording { Id = Guid.NewGuid(), Service = HarmonyDB.Shelf.Database.Model.MusicService.Youtube, ServiceId = Guid.NewGuid().ToString("N"), UpdatedAt = DateTimeOffset.UtcNow };
            var chords = new HarmonyDB.Shelf.Database.Model.ChordsItem { Id = Guid.NewGuid(), SourceId = Id(), CreatedAt = DateTimeOffset.UtcNow };
            db.AddRange(recording, chords);
            db.PlaylistSongItems.AddRange(
                new() { Id = found, SongId = song, RecordingId = recording.Id, Position = 5, AddedAt = DateTimeOffset.UtcNow },
                new() { Id = imported, SongId = song, ChordsId = chords.Id, Position = 6, AddedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        await Ok<UndoOnly>(bob, HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{song}/variants/{found}");
        await Browser.AssertError(await bob.SendAsync(HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{song}/variants/{imported}"), HttpStatusCode.Forbidden, PlaylistErrorCodes.NotYours);

        // a song left with only recordings found for it is no song: removing its chords removes it; clearing what one
        // added leaves no husk either
        var other = await AddAsync(ann, playlist, Id(), "Other");
        var husk = await AddAsync(ann, playlist, Id(), "Husk");
        await using (var db = fixture.CreateContext())
        {
            foreach (var target in new[] { other, husk })
            {
                var recording = new HarmonyDB.Shelf.Database.Model.Recording { Id = Guid.NewGuid(), Service = HarmonyDB.Shelf.Database.Model.MusicService.Youtube, ServiceId = Guid.NewGuid().ToString("N"), UpdatedAt = DateTimeOffset.UtcNow };
                db.Recordings.Add(recording);
                db.PlaylistSongItems.Add(new() { Id = Guid.NewGuid(), SongId = target, RecordingId = recording.Id, Position = 5, AddedAt = DateTimeOffset.UtcNow });
            }

            await db.SaveChangesAsync();
        }

        var chordsOfOther = (await PageAsync(ann, playlist, other)).Variants.Single().ItemId;
        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{other}/variants/{chordsOfOther}");
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs/{other}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchSong);
        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/mine?added=true");
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}/songs/{husk}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchSong);
    }

    [DatabaseFact]
    public async Task TheSmallerRulesOfRoundTwo()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var guest = fixture.Api.NewBrowser();
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        var held = Id();
        await AddAsync(ann, playlist, held, "Held");

        // a source that does not answer fails only the change that needed it; chords held here need no asking
        var batch = await SyncAsync(ann, null,
            new Change { Type = ChangeType.Mark, Chords = $"away-{Guid.NewGuid():N}", Kind = "star", On = true },
            new Change { Type = ChangeType.Mark, Chords = held, Kind = "star", On = true });
        Assert.Equal([(false, ErrorCodes.Unavailable), (true, null)], batch.Results.Select(x => (x.Applied, x.Code)));

        // a watermark this server never gave: everything
        Assert.Equal([held], (await SyncAsync(ann, long.MaxValue / 2)).Marks.Select(x => x.Chords));

        // a song removed on purpose: its link leads nowhere, also when its chords come back in another song
        var chords = Id();
        var removed = await AddAsync(ann, playlist, chords, "Removed");
        var link = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Song, SongId = removed });
        await Ok<UndoOnly>(ann, HttpMethod.Delete, $"/api/playlists/{playlist}/songs/{removed}");
        await AddAsync(ann, playlist, chords, "Again");
        await Browser.AssertError(await guest.SendAsync(HttpMethod.Get, $"/api/share/{link.Token}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchLink);

        // a large playlist, joined, starts out of the library, and the answer says so
        for (var i = 0; i < 4; i++)
            await AddAsync(ann, playlist, Id(), $"More {i}");
        var invitation = (await Ok<Changed<InvitationItem>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/invitations", new CreateInvitationRequest())).Value;
        var joined = (await Ok<Changed<JoinedPlaylist>>(bob, HttpMethod.Post, $"/api/playlists/invitations/{invitation.Token}/accept")).Value;
        Assert.True(joined.KeptOutOfLibrary);
        Assert.False(joined.Playlist.ShowInLibrary);
        Assert.DoesNotContain((await Ok<SongList>(bob, HttpMethod.Get, "/api/library")).Rows, x => x.PlaylistId == playlist);
    }

    [DatabaseFact]
    public async Task FollowingKeepsAPlaylistWithoutJoiningIt()
    {
        var (ann, annId) = await fixture.PersonAsync("ann");
        var (carol, carolId) = await fixture.PersonAsync("carol");
        var playlist = await CreateAsync(ann, PlaylistAccess.Public);
        var chords = Id();
        var song = await AddAsync(ann, playlist, chords, "Common song");
        await SyncAsync(ann, null,
            new Change { Type = ChangeType.Mark, Chords = chords, Kind = "heart_red", On = true },
            new Change { Type = ChangeType.Key, Chords = chords, PlaylistId = playlist, Semitones = 2 });

        var followed = await Ok<Changed<JoinedPlaylist>>(carol, HttpMethod.Post, $"/api/playlists/{playlist}/follow");
        Assert.True(followed.Value.Playlist.Following);
        Assert.Equal((null, true, false), (followed.Value.Playlist.MyRole, followed.Value.Playlist.ShowInLibrary, followed.Value.KeptOutOfLibrary));
        Assert.DoesNotContain(followed.Value.Members, x => x.Person.Id == carolId);
        Assert.Contains((await Ok<List<PlaylistSummary>>(carol, HttpMethod.Get, "/api/playlists")), x => x.Id == playlist && x.Following);
        Assert.Contains((await Ok<SongList>(carol, HttpMethod.Get, "/api/library")).Rows, x => x.SongId == song);

        // not a member: adds nothing; their marks and the members' are not seen by each other; the "here" key is
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = Id(), Title = "x" }), HttpStatusCode.Forbidden, PlaylistErrorCodes.MembersOnly);
        await SyncAsync(carol, null, new Change { Type = ChangeType.Mark, Chords = chords, Kind = "star", On = true });
        var carols = await PageAsync(carol, playlist, song);
        Assert.Equal([carolId], carols.Marks.Select(x => x.AccountId));
        Assert.Equal([(annId, 2, true)], carols.Keys.Select(x => (x.AccountId, x.Semitones, x.Here)));
        Assert.DoesNotContain((await PageAsync(ann, playlist, song)).Marks, x => x.AccountId == carolId);
        var row = (await Ok<SongList>(carol, HttpMethod.Get, "/api/library")).Rows.Single(x => x.SongId == song);
        Assert.Equal(0, row.OtherPeople);
        Assert.Equal([chords], row.ChordsIds);

        // its own "show in my library"; following again changes nothing
        await Ok<Changed<PlaylistDetails>>(carol, HttpMethod.Put, $"/api/playlists/{playlist}/library", new ShowInLibraryRequest { Show = false });
        Assert.DoesNotContain((await Ok<SongList>(carol, HttpMethod.Get, "/api/library")).Rows, x => x.SongId == song);
        Assert.Null((await Ok<Changed<JoinedPlaylist>>(carol, HttpMethod.Post, $"/api/playlists/{playlist}/follow")).Undo);
        await Ok<Changed<PlaylistDetails>>(carol, HttpMethod.Put, $"/api/playlists/{playlist}/library", new ShowInLibraryRequest { Show = true });

        // made private, it drops out for its followers; public again, it is back
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Access = PlaylistAccess.Private });
        Assert.DoesNotContain((await Ok<List<PlaylistSummary>>(carol, HttpMethod.Get, "/api/playlists")), x => x.Id == playlist);
        Assert.DoesNotContain((await Ok<SongList>(carol, HttpMethod.Get, "/api/library")).Rows, x => x.SongId == song);
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchPlaylist);

        // a link playlist: followed by its link, then kept without it
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{playlist}", new UpdatePlaylistRequest { Access = PlaylistAccess.Link });
        Assert.True((await Ok<PlaylistDetails>(carol, HttpMethod.Get, $"/api/playlists/{playlist}")).Playlist.Following);
        Assert.Contains((await Ok<SongList>(carol, HttpMethod.Get, "/api/library")).Rows, x => x.SongId == song);

        // unfollowed, gone; undone, back
        var unfollowed = await Ok<UndoOnly>(carol, HttpMethod.Delete, $"/api/playlists/{playlist}/follow");
        Assert.DoesNotContain((await Ok<List<PlaylistSummary>>(carol, HttpMethod.Get, "/api/playlists")), x => x.Id == playlist);
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Get, $"/api/playlists/{playlist}"), HttpStatusCode.NotFound, PlaylistErrorCodes.NoSuchPlaylist);
        await UndoAsync(carol, unfollowed.Undo);
        Assert.Contains((await Ok<List<PlaylistSummary>>(carol, HttpMethod.Get, "/api/playlists")), x => x.Id == playlist);

        // a large one starts out of the library, and the answer says so; a member does not follow
        var (dan, _) = await fixture.PersonAsync("dan");
        for (var i = 0; i < 5; i++)
            await AddAsync(ann, playlist, Id(), $"More {i}");
        var link = await Ok<ShareLinkItem>(ann, HttpMethod.Post, "/api/share", new CreateShareRequest { Kind = ShareKind.Playlist, PlaylistId = playlist });
        var large = (await Ok<Changed<JoinedPlaylist>>(dan, HttpMethod.Post, $"/api/playlists/{playlist}/follow?share={link.Token}")).Value;
        Assert.Equal((true, false), (large.KeptOutOfLibrary, large.Playlist.ShowInLibrary));
        await Browser.AssertError(await ann.SendAsync(HttpMethod.Post, $"/api/playlists/{playlist}/follow"), HttpStatusCode.Conflict, PlaylistErrorCodes.AlreadyMember);
    }

    [DatabaseFact]
    public async Task TheSyncRefusesAnotherAccountsQueue()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (_, bobId) = await fixture.PersonAsync("bob");
        await Browser.AssertError(
            await ann.SendAsync(HttpMethod.Post, "/api/sync", new SyncRequest { AccountId = bobId, Changes = [new() { Type = ChangeType.Mark, Chords = Id(), Kind = "star", On = true }] }),
            HttpStatusCode.Conflict, PlaylistErrorCodes.WrongAccount);
        Assert.Empty((await SyncAsync(ann, null)).Marks);
    }

    [DatabaseFact]
    public async Task ASongsCoverIsAnIllustrationFirstThenARecording()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (bob, _) = await fixture.PersonAsync("bob");
        var playlist = await CreateAsync(ann, PlaylistAccess.Private);
        await JoinAsync(ann, bob, playlist, PlaylistRole.Member);
        string a = Id(), b = Id(), plain = Id(), bare = Id();
        var song = await AddAsync(ann, playlist, a, "Illustrated");
        await Ok<Changed<SongRef>>(ann, HttpMethod.Post, $"/api/playlists/{playlist}/songs/{song}/variants", new AddVariantRequest { Chords = b });
        var recorded = await AddAsync(ann, playlist, plain, "Recorded");
        var none = await AddAsync(ann, playlist, bare, "Nothing");
        Guid pictureA = Guid.NewGuid(), pictureB = Guid.NewGuid();
        lock (fixture.Illustrations.Covers)
        {
            fixture.Illustrations.Covers[a] = pictureA;
            fixture.Illustrations.Covers[b] = pictureB;
        }

        await using (var db = fixture.CreateContext())
        {
            var recording = new HarmonyDB.Shelf.Database.Model.Recording { Id = Guid.NewGuid(), Service = HarmonyDB.Shelf.Database.Model.MusicService.Youtube, ServiceId = Guid.NewGuid().ToString("N"), CoverUrl = "https://covers.test/r.jpg", UpdatedAt = DateTimeOffset.UtcNow };
            db.Recordings.Add(recording);
            foreach (var target in new[] { song, recorded })
                db.PlaylistSongItems.Add(new() { Id = Guid.NewGuid(), SongId = target, RecordingId = recording.Id, Position = 9, AddedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        // the main variant's illustration, for each person: Bob's is the first variant's, Ann chose the second
        var variants = (await PageAsync(ann, playlist, song)).Variants;
        await Ok<UndoOnly>(ann, HttpMethod.Put, $"/api/playlists/{playlist}/songs/{song}/main", new SetMainVariantRequest { ItemId = variants[1].ItemId });
        var anns = (await Ok<SongList>(ann, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.ToDictionary(x => x.SongId!.Value);
        var bobs = (await Ok<SongList>(bob, HttpMethod.Get, $"/api/playlists/{playlist}/songs")).Rows.ToDictionary(x => x.SongId!.Value);
        Assert.Equal($"/api/illustrations/images/{pictureB}/256", anns[song].Cover);
        Assert.Equal($"/api/illustrations/images/{pictureA}/256", bobs[song].Cover);

        // no illustration: the recording's cover; neither: none
        Assert.Equal("https://covers.test/r.jpg", anns[recorded].Cover);
        Assert.Null(anns[none].Cover);

        // the playlist: its first songs' covers, in order
        var summary = (await Ok<PlaylistDetails>(bob, HttpMethod.Get, $"/api/playlists/{playlist}")).Playlist;
        Assert.Equal([$"/api/illustrations/images/{pictureA}/256", "https://covers.test/r.jpg"], summary.Covers);
    }

    [DatabaseFact]
    public async Task FollowsEndWithTheProfileAndMoveInAMerge()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var first = $"follow-first-{Guid.NewGuid():N}";
        var second = $"follow-second-{Guid.NewGuid():N}";
        var firstBrowser = fixture.Api.NewBrowser();
        await firstBrowser.DevAsync(first);
        var firstId = (await firstBrowser.GetAsync<Profile>("/api/me")).Id;
        var secondBrowser = fixture.Api.NewBrowser();
        await secondBrowser.DevAsync(second);
        var secondId = (await secondBrowser.GetAsync<Profile>("/api/me")).Id;

        // the first is a member of one playlist, the second follows it and another
        var joint = await CreateAsync(ann, PlaylistAccess.Public);
        await JoinAsync(ann, firstBrowser, joint, PlaylistRole.Member);
        var other = await CreateAsync(ann, PlaylistAccess.Public);
        await Ok<Changed<JoinedPlaylist>>(secondBrowser, HttpMethod.Post, $"/api/playlists/{joint}/follow");
        await Ok<Changed<JoinedPlaylist>>(secondBrowser, HttpMethod.Post, $"/api/playlists/{other}/follow");

        // merged: the follow of the one the first is in goes; the other moves
        await firstBrowser.DevAsync(second, SignInPurpose.Link);
        await using (var db = fixture.CreateContext())
        {
            var state = await db.SignInFlows.Where(x => x.Session!.AccountId == firstId && x.CompletedLoginId != null).Select(x => x.State).SingleAsync();
            Assert.Equal(HttpStatusCode.OK, (await firstBrowser.SendAsync(HttpMethod.Post, $"/api/auth/flows/{state}/merge")).StatusCode);
            Assert.Equal([other], await db.PlaylistFollows.Where(x => x.AccountId == firstId).Select(x => x.PlaylistId).ToListAsync());
            Assert.False(await db.PlaylistFollows.AnyAsync(x => x.AccountId == secondId));
        }

        var mine = await Ok<List<PlaylistSummary>>(firstBrowser, HttpMethod.Get, "/api/playlists");
        Assert.Contains(mine, x => x.Id == other && x.Following);
        Assert.Contains(mine, x => x.Id == joint && x.MyRole == PlaylistRole.Member);

        // the profile deleted: its follows end
        Assert.Equal(HttpStatusCode.NoContent, (await firstBrowser.SendAsync(HttpMethod.Delete, "/api/me")).StatusCode);
        await using (var db = fixture.CreateContext())
            Assert.False(await db.PlaylistFollows.AnyAsync(x => x.AccountId == firstId));
    }

    [DatabaseFact]
    public async Task AFollowCountsOnlyWhereThePersonIsNoMember()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (carol, _) = await fixture.PersonAsync("carol");
        var playlist = await CreateAsync(ann, PlaylistAccess.Public);
        var song = await AddAsync(ann, playlist, Id(), "Song");

        // followed, unfollowed, joined, kept out of the library: bringing the follow back is nothing to undo
        await Ok<Changed<JoinedPlaylist>>(carol, HttpMethod.Post, $"/api/playlists/{playlist}/follow");
        var unfollowed = await Ok<UndoOnly>(carol, HttpMethod.Delete, $"/api/playlists/{playlist}/follow");
        await JoinAsync(ann, carol, playlist, PlaylistRole.Member);
        await Ok<Changed<PlaylistDetails>>(carol, HttpMethod.Put, $"/api/playlists/{playlist}/library", new ShowInLibraryRequest { Show = false });
        await Browser.AssertError(await carol.SendAsync(HttpMethod.Post, "/api/sync/undo", new UndoRequest { Token = unfollowed.Undo! }), HttpStatusCode.Conflict, PlaylistErrorCodes.NothingToUndo);
        Assert.DoesNotContain((await Ok<SongList>(carol, HttpMethod.Get, "/api/library")).Rows, x => x.SongId == song);

        // a follow left from before (as a merge may leave one) changes nothing for a member
        await using (var db = fixture.CreateContext())
        {
            var carolId = (await carol.GetAsync<Profile>("/api/me")).Id;
            db.PlaylistFollows.Add(new() { Id = Guid.NewGuid(), PlaylistId = playlist, AccountId = carolId, ShowInLibrary = true, FollowedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain((await Ok<SongList>(carol, HttpMethod.Get, "/api/library")).Rows, x => x.SongId == song);
        Assert.Single((await Ok<List<PlaylistSummary>>(carol, HttpMethod.Get, "/api/playlists")), x => x.Id == playlist);
    }

    [DatabaseFact]
    public async Task AnExFollowerHearsNoMoreOfAPrivatePlaylist()
    {
        var (ann, _) = await fixture.PersonAsync("ann");
        var (dan, _) = await fixture.PersonAsync("dan");
        var closing = await CreateAsync(ann, PlaylistAccess.Public);
        var open = await CreateAsync(ann, PlaylistAccess.Public);
        await Ok<Changed<JoinedPlaylist>>(dan, HttpMethod.Post, $"/api/playlists/{closing}/follow");
        await Ok<Changed<JoinedPlaylist>>(dan, HttpMethod.Post, $"/api/playlists/{open}/follow");
        var start = await SyncAsync(dan, null);

        // made private: that change is told
        await Ok<Changed<PlaylistDetails>>(ann, HttpMethod.Patch, $"/api/playlists/{closing}", new UpdatePlaylistRequest { Access = PlaylistAccess.Private });
        var told = await SyncAsync(dan, start.Watermark);
        Assert.Contains(closing, told.Playlists);

        // what happens inside it then is not; inside a playlist still open, it is. The sync may give a change again (the
        // watermark is the oldest transaction still open anywhere on the server), so the private one is asked the same
        // before and after the song is added: the song changes nothing of the answer
        var before = (await SyncAsync(dan, told.Watermark)).Playlists.Contains(closing);
        await AddAsync(ann, closing, Id(), "Inside");
        await AddAsync(ann, open, Id(), "Inside too");
        var after = await SyncAsync(dan, told.Watermark);
        Assert.Equal(before, after.Playlists.Contains(closing));
        Assert.Contains(open, after.Playlists);
    }

    private async Task WaitForSyncAsync(Browser browser, Guid playlist)
    {
        for (var i = 0; i < 100; i++)
        {
            var sync = (await Ok<PlaylistDetails>(browser, HttpMethod.Get, $"/api/playlists/{playlist}")).Sync;
            if (sync is { Status: JobStatus.Succeeded or JobStatus.Failed })
            {
                Assert.Equal(JobStatus.Succeeded, sync.Status);
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("The sync did not finish.");
    }

    private static string Id() => $"test-{Guid.NewGuid():N}";

    private static async Task<Guid> CreateAsync(Browser owner, PlaylistAccess access, PlaylistAddPolicy policy = PlaylistAddPolicy.AllMembers)
        => (await Ok<Changed<PlaylistDetails>>(owner, HttpMethod.Post, "/api/playlists", new CreatePlaylistRequest { Title = "A playlist", Access = access, AddPolicy = policy })).Value.Playlist.Id;

    private static async Task JoinAsync(Browser owner, Browser joining, Guid playlist, PlaylistRole role)
    {
        var invitation = (await Ok<Changed<InvitationItem>>(owner, HttpMethod.Post, $"/api/playlists/{playlist}/invitations", new CreateInvitationRequest { Role = role })).Value;
        await Ok<Changed<PlaylistDetails>>(joining, HttpMethod.Post, $"/api/playlists/invitations/{invitation.Token}/accept");
    }

    private static async Task<Guid> AddAsync(Browser browser, Guid playlist, string chords, string title)
        => (await Ok<Changed<SongRef>>(browser, HttpMethod.Post, $"/api/playlists/{playlist}/songs", new AddSongRequest { Chords = chords, Title = title })).Value.Id;

    private static Task<SongPage> PageAsync(Browser browser, Guid playlist, Guid song)
        => Ok<SongPage>(browser, HttpMethod.Get, $"/api/playlists/{playlist}/songs/{song}");

    private static async Task<SyncResponse> SyncAsync(Browser browser, long? since, params Change[] changes)
        => await Ok<SyncResponse>(browser, HttpMethod.Post, "/api/sync", new SyncRequest { AccountId = (await browser.GetAsync<Profile>("/api/me")).Id, Since = since, Changes = changes });

    private static async Task UndoAsync(Browser browser, string? token)
    {
        Assert.NotNull(token);
        var response = await browser.SendAsync(HttpMethod.Post, "/api/sync/undo", new UndoRequest { Token = token });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<T> Ok<T>(Browser browser, HttpMethod method, string path, object? body = null)
    {
        var response = await browser.SendAsync(method, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
