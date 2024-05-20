namespace HarmonyDB.Index.Api.Model.VPublic;

public class GetPublicResponse
{
    public string? Html { get; init; }
    public string? Title { get; set; }
    public List<string> Artists { get; set; }
    public string? Error { get; set; }
    public string? Redirect { get; set; }
}