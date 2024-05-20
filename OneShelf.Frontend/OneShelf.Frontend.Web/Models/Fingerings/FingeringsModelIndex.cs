namespace OneShelf.Frontend.Web.Models.Fingerings;

public class FingeringsModelIndex
{
    public FingeringsModel Model { get; init; }

    public IReadOnlyDictionary<string, FingeringsChordType> TypesByChordText { get; init; }
}