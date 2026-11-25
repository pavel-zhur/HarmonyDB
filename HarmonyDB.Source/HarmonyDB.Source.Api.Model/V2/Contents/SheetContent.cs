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
}
