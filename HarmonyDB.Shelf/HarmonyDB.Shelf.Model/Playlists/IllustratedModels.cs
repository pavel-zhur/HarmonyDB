namespace HarmonyDB.Shelf.Model.Playlists;

/// <summary>
/// The chords of the person's library that have an illustration, by title: a page of them.
/// </summary>
public record IllustratedChordsList
{
    public required int Total { get; init; }

    public required int Offset { get; init; }

    public required IReadOnlyList<IllustratedChords> Items { get; init; }
}

/// <summary>
/// Chords with an illustration, as a tile shows them.
/// </summary>
public record IllustratedChords
{
    /// <summary>
    /// The chords' id, as they are opened (<c>GET /api/chords/{id}</c>).
    /// </summary>
    public required string Chords { get; init; }

    public string? Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    /// <summary>
    /// The illustration's picture, as the covers are given elsewhere (<c>/api/illustrations/images/…</c>); the chords'
    /// other illustrations are at <c>GET /api/illustrations/{chords}</c>.
    /// </summary>
    public required string PictureUrl { get; init; }
}
