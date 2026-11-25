using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Playlists;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Common;

/// <summary>
/// The marks, keys and notes a person sees on chords, by the rules of who sees whose:
/// <list type="bullet">
/// <item>a person sees their own marks, their "mine" keys and their personal notes everywhere;</item>
/// <item>in a playlist the person is a member of: also the other members' marks and their keys ("mine" and "here");</item>
/// <item>the members' "here" keys in a playlist and the notes they wrote in it are the playlist's content: seen by
/// its members, and, in a <c>public</c> or <c>link</c> playlist, by whoever sees it (guests too);</item>
/// <item>a guest sees nothing else.</item>
/// </list>
/// Others' marks and "mine" keys are seen only in a playlist where both are members.
/// </summary>
public sealed class ChordsAnnotations(ShelfDbContext db)
{
    public sealed record Result(
        IReadOnlyList<MarkItem> Marks,
        IReadOnlyList<KeyItem> Keys,
        IReadOnlyList<SongNote> SongNotes,
        IReadOnlyList<ChordsNote> MyNotes,
        IReadOnlyList<Guid> People);

    public static readonly Result None = new([], [], [], [], []);

    /// <summary>
    /// The person's own on the chords, seen outside a playlist.
    /// </summary>
    public Task<Result> ReadAsync(Guid? viewer, IReadOnlyCollection<Guid> chordsIds, Guid? playlistId, Guid? songId, CancellationToken cancellationToken)
        => ReadAsync(viewer, chordsIds, playlistId, songId, false, cancellationToken);

    /// <param name="viewer">The person; null for a guest.</param>
    /// <param name="chordsIds">The chords' local ids.</param>
    /// <param name="playlistId">The playlist they are seen in, if they are; others' marks and keys are given only when
    /// the viewer is its member.</param>
    /// <param name="songId">The song of that playlist they are seen in, for its notes.</param>
    /// <param name="seesPlaylist">Whether the caller has been found to see the playlist (by its access, or by a link to
    /// it): a <c>public</c> or <c>link</c> playlist's "here" keys and notes are then given to a non-member too.</param>
    public async Task<Result> ReadAsync(
        Guid? viewer, IReadOnlyCollection<Guid> chordsIds, Guid? playlistId, Guid? songId, bool seesPlaylist, CancellationToken cancellationToken)
    {
        if (chordsIds.Count == 0)
            return None;

        var ids = chordsIds.Distinct().ToList();
        var member = viewer != null && playlistId != null && await db.PlaylistMembers.AnyAsync(
            x => x.PlaylistId == playlistId && x.AccountId == viewer && x.Playlist.DeletedAt == null, cancellationToken);
        var content = member || (seesPlaylist && playlistId != null
                                               && await db.Playlists.AnyAsync(x => x.Id == playlistId && x.Access != Database.Model.PlaylistAccess.Private, cancellationToken));
        if (viewer == null && !content)
            return None;

        var members = content
            ? await db.PlaylistMembers.Where(x => x.PlaylistId == playlistId).Select(x => x.AccountId).ToListAsync(cancellationToken)
            : [];
        // whose marks and "mine" keys: the members' to a member, one's own to anyone else
        List<Guid> close = member ? members : viewer is { } me ? [me] : [];

        var marks = await db.Marks
            .Where(x => close.Contains(x.AccountId) && ids.Contains(x.ChordsId))
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MarkItem { AccountId = x.AccountId, Chords = x.Chords.SourceId, Kind = x.Kind, CreatedAt = x.CreatedAt })
            .ToListAsync(cancellationToken);

        var keys = await db.Transpositions
            .Where(x => ids.Contains(x.ChordsId)
                        && ((x.PlaylistId == null && close.Contains(x.AccountId))
                            || (content && x.PlaylistId == playlistId && members.Contains(x.AccountId))))
            .Select(x => new KeyItem { AccountId = x.AccountId, Chords = x.Chords.SourceId, Semitones = x.Semitones, Here = x.PlaylistId != null })
            .ToListAsync(cancellationToken);

        var songNotes = content && songId != null
            ? await db.Notes.Where(x => x.SongId == songId && x.Song!.PlaylistId == playlistId && members.Contains(x.AccountId))
                .OrderBy(x => x.UpdatedAt)
                .Select(x => new SongNote { AccountId = x.AccountId, Text = x.Text, UpdatedAt = x.UpdatedAt })
                .ToListAsync(cancellationToken)
            : [];

        var myNotes = viewer == null
            ? []
            : await db.Notes.Where(x => x.AccountId == viewer && x.ChordsId != null && ids.Contains(x.ChordsId.Value))
                .Select(x => new ChordsNote { Chords = x.Chords!.SourceId, Text = x.Text, UpdatedAt = x.UpdatedAt })
                .ToListAsync(cancellationToken);

        var named = marks.Select(x => x.AccountId).Concat(keys.Select(x => x.AccountId)).Concat(songNotes.Select(x => x.AccountId)).Concat(viewer is { } self ? [self] : []).Distinct().ToList();
        return new(marks, keys, songNotes, myNotes, named);
    }
}
