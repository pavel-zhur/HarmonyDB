using HarmonyDB.Common.Representations.OneShelf;

namespace HarmonyDB.Source.Api.Model.V2.Contents;

/// <summary>
/// Text with chords: the chord sheet.
/// </summary>
public record SheetContent
{
    public required NodeHtml Root { get; init; }

    /// <summary>
    /// What the author of these chords wrote about them, as plain text, when there is something.
    /// </summary>
    public string? AuthorComment { get; init; }

    /// <summary>
    /// Present when the source does not vouch for this sheet: its words, shown in the sheet's place with the item's
    /// address; such a sheet is not printed.
    /// </summary>
    public string? UnstableMessage { get; init; }
}
