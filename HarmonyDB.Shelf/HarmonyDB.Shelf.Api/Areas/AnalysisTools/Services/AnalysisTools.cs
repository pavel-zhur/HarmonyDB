using System.Security.Claims;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model.AnalysisTools;
using HarmonyDB.Shelf.Model.Chords;

namespace HarmonyDB.Shelf.Api.Areas.AnalysisTools.Services;

/// <summary>
/// The index's analysis tools for the person: an item's detected keys and loops, and the items with a progression.
/// The index knows nothing of who asks, so an item is analysed only when it is open to the person, and what the index
/// finds is shown only as far as the sources open it to the person.
/// </summary>
public sealed class AnalysisTools(IndexClient index, ChordsReader chords)
{
    public const int PerPage = 50;

    public async Task<ChordsAnalysis> GetAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        var answer = (await chords.GetHeadersAsync(user, [id], cancellationToken)).Single();
        if (answer.State != ChordsState.Open)
            return new() { Id = id, State = answer.State, Analysed = false, Keys = [], Loops = [] };

        var song = await index.GetSongAsync(id, cancellationToken);
        if (song == null)
            return new() { Id = id, State = ChordsState.Open, Analysed = false, Keys = [], Loops = [] };

        var links = song.Links.Where(x => x.ExternalId == id).ToDictionary(x => x.Normalized);
        return new()
        {
            Id = id,
            State = ChordsState.Open,
            Analysed = true,
            Keys = DetectedKeys(song.Song.Probabilities, IndexKeys.Key),
            StatedKey = song.Song.KnownTonalityIndex is { } known ? IndexKeys.Key(known) : null,
            TonicScore = song.Song.TonicScore,
            ScaleScore = song.Song.ScaleScore,
            Loops = song.Loops
                .Where(x => links.ContainsKey(x.Normalized))
                .Select(x => (loop: x, link: links[x.Normalized]))
                .OrderByDescending(x => x.link.Coverage)
                .Select(x => new ChordsLoop
                {
                    Normalized = x.loop.Normalized,
                    Length = x.loop.Length,
                    NormalizationRoot = IndexKeys.NoteName(x.link.NormalizationRoot),
                    Occurrences = x.link.Occurrences,
                    Successions = x.link.Successions,
                    Coverage = x.link.Coverage,
                    SongsWithIt = x.loop.TotalSongs,
                    Keys = DetectedKeys(x.loop.Probabilities, k => IndexKeys.LoopKey(k, x.link.NormalizationRoot)),
                })
                .ToList(),
        };
    }

    /// <param name="progression">Chord names, space-separated ("Am F C G").</param>
    /// <param name="page">From 1.</param>
    public async Task<SimilarChords> GetSimilarAsync(ClaimsPrincipal user, string progression, int page, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(progression) || page < 1)
            throw ShelfException.BadRequest();

        var found = await index.SongsByChordsAsync(progression.Trim(), page, PerPage, cancellationToken);
        var answers = found.Songs.Count == 0
            ? []
            : await chords.GetHeadersAsync(user, found.Songs.Select(x => x.Header.ExternalId).ToList(), cancellationToken);

        return new()
        {
            Page = found.CurrentPageNumber,
            TotalPages = found.TotalPages,
            Items = found.Songs.Zip(answers)
                .Where(x => x.Second.State == ChordsState.Open)
                .Select(x => new SimilarChordsItem
                {
                    Header = x.Second.Header!,
                    Coverage = x.First.Coverage,
                    DetectedKey = x.First.PredictedTonalityIndex is { } key ? IndexKeys.Key(key) : null,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// The best key, and the second when it is at least half as likely.
    /// </summary>
    /// <param name="key">The key of the index's number of a key.</param>
    public static IReadOnlyList<DetectedKey> DetectedKeys(float[] probabilities, Func<int, SheetKey> key)
    {
        var ordered = probabilities
            .Select((confidence, index) => (confidence, index))
            .OrderByDescending(x => x.confidence)
            .Take(2)
            .Select(x => new DetectedKey { Key = key(x.index), Confidence = x.confidence })
            .ToList();

        return ordered.Count == 2 && ordered[1].Confidence < ordered[0].Confidence / 2 ? [ordered[0]] : ordered;
    }
}
