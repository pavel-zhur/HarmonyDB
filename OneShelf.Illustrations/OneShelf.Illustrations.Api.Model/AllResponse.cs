namespace OneShelf.Illustrations.Api.Model;

public class AllResponse
{
    public required Dictionary<string, OneResponse> Responses { get; set; }
    
    public required IReadOnlyDictionary<int, string> SystemMessages { get; set; }

    public required Dictionary<int, AlteredVersion> AlteredVersions { get; set; }
    
    public required Dictionary<string, AvailableAlteration> Alterations { get; set; }
}