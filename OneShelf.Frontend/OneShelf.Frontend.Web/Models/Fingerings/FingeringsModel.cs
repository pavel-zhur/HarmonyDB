namespace OneShelf.Frontend.Web.Models.Fingerings;

public class FingeringsModel
{
    public required IReadOnlyList<FingeringsChordType> Types { get; init; }

    public required IReadOnlyDictionary<string, byte[]> Pictures { get; init; }
}