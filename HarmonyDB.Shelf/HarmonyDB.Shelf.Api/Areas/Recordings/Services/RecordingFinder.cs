using System.Text;
using System.Text.RegularExpressions;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Music;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Recordings.Services;

/// <summary>
/// Finds the recordings of an item of chords on both music services, by its title and artists, and keeps them in
/// <c>chords_recordings</c>, best first, for everyone. A track is taken when its title is the chords' title (or one holds
/// the other) and one of its artists is one of theirs. It is a guess all the same.
/// The search is made once; when a service did not answer, it is made again the next time.
/// The title and artists come from the header of the person it is done for, who was just given the item open: never
/// from the chords' row (<see cref="ItemRows"/>); the recordings found are shown only where the chords are open.
/// </summary>
public sealed partial class RecordingFinder(ShelfDbContext db, MusicCatalog music, ItemRows rows, TimeProvider time)
{
    private const int KeptPerService = 5;

    /// <param name="chords">The chords' row.</param>
    /// <param name="header">The chords' header as the person was just given it, open.</param>
    /// <returns>Whether every service answered.</returns>
    public async Task<bool> EnsureFoundAsync(ChordsItem chords, Header header, CancellationToken cancellationToken)
    {
        if (chords.RecordingsSearchedAt != null)
            return true;

        // a title alone names too many songs: chords with no artist get no guesses
        var title = Normalize(header.Title ?? string.Empty);
        var artists = header.Artists.Select(Normalize).Where(x => x.Length > 0).ToList();
        if (title.Length == 0 || artists.Count == 0)
            return true;

        var text = string.Join(' ', header.Artists.Take(2).Append(header.Title));

        var complete = true;
        var taken = new List<(int Score, int Order, MusicTrack Track)>();
        foreach (var service in new[] { Model.Me.MusicService.Yandex, Model.Me.MusicService.Youtube })
        {
            IReadOnlyList<MusicTrack> tracks;
            try
            {
                (tracks, _) = await music.SearchAsync(service, text, cancellationToken);
            }
            catch (ShelfException)
            {
                complete = false;
                continue;
            }

            taken.AddRange(tracks
                .Select((track, order) => (score: Score(title, artists, track), order, track))
                .Where(x => x.score > 0)
                .Take(KeptPerService));
        }

        var rank = 0;
        var now = time.GetUtcNow();
        foreach (var (_, _, track) in taken.OrderByDescending(x => x.Score).ThenBy(x => x.Order))
        {
            var recording = await rows.GetOrCreateRecordingAsync(MusicShapes.ToDatabase(track.Service), track.Id, Describe(track), cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO chords_recordings (chords_id, recording_id, rank, found_at) VALUES ({chords.Id}, {recording.Id}, {rank++}, {now}) ON CONFLICT (chords_id, recording_id) DO NOTHING",
                cancellationToken);
        }

        if (complete)
        {
            chords.RecordingsSearchedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return complete;
    }

    public static RecordingDescription Describe(MusicTrack track)
        => new(track.Title, track.Artists.Select(x => x.Name).OfType<string>().ToList(), track.DurationMs / 1000.0, track.CoverUrl);

    /// <summary>
    /// 0 when the track is not taken; more the closer it is.
    /// </summary>
    public static int Score(string title, IReadOnlyList<string> artists, MusicTrack track)
    {
        var trackTitle = Normalize(track.Title ?? string.Empty);
        if (trackTitle.Length == 0 || title.Length == 0) return 0;

        var titleScore = trackTitle == title ? 3
            : Math.Min(trackTitle.Length, title.Length) >= 3 && (trackTitle.Contains(title) || title.Contains(trackTitle)) ? 1
            : 0;
        if (titleScore == 0) return 0;

        var trackArtists = track.Artists.Select(x => Normalize(x.Name ?? string.Empty)).Where(x => x.Length > 0).ToList();
        var artistMatches = trackArtists.Any(t => artists.Any(a => t == a || (Math.Min(t.Length, a.Length) >= 3 && (t.Contains(a) || a.Contains(t)))));
        return artistMatches ? titleScore + 2 : 0;
    }

    /// <summary>
    /// Lower case, ё as е, without what is in brackets, letters and digits only.
    /// </summary>
    public static string Normalize(string value)
    {
        var text = Brackets().Replace(value.ToLowerInvariant().Replace('ё', 'е'), " ");
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
            result.Append(char.IsLetterOrDigit(c) ? c : ' ');

        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex Brackets();
}
