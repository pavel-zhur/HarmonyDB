namespace OneShelf.Frontend.Web.Models.Fingerings;

public class FingeringsChordType
{
    public required string Description { get; init; }

    public required string ChordHtml { get; init; }

    public required string ChordText { get; init; }

    public required string Url { get; init; }

    public required IReadOnlyDictionary<FingeringsNote, IReadOnlyList<string>> Notes { get; init; }
}