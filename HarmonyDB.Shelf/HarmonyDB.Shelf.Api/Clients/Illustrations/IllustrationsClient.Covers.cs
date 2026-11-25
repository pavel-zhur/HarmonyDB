namespace HarmonyDB.Shelf.Api.Clients.Illustrations;

/// <summary>
/// The covers of many chords at once, for the rows of a list.
/// </summary>
public sealed partial class IllustrationsClient
{
    /// <summary>
    /// The most keys asked in one call.
    /// </summary>
    public const int MaxCoverKeys = 1000;

    /// <summary>
    /// For each key (chords' id) that has illustrations, the image to show as its cover.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, Guid>> GetCoversAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        var covers = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var chunk in keys.Distinct(StringComparer.Ordinal).Chunk(MaxCoverKeys))
        {
            foreach (var cover in (await PostAsync<CoversAnswer>("ShelfCovers", new { keys = chunk }, _options.Timeout, cancellationToken)).Covers)
                covers[cover.Key] = cover.Id;
        }

        return covers;
    }

    private sealed record CoversAnswer(IReadOnlyList<CoverAnswer> Covers);

    private sealed record CoverAnswer(string Key, Guid Id);
}
