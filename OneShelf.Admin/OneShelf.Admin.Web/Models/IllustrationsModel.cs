using OneShelf.Illustrations.Api.Model;

namespace OneShelf.Admin.Web.Models;

public class IllustrationsModel
{
    public IllustrationsModel(AllResponse results,
        Dictionary<string, string> urlsToTitles, int hidden, Mode currentMode, int countPhotos, int? justVersion)
    {
        Results = results;
        UrlsToTitles = urlsToTitles;
        Hidden = hidden;
        CurrentMode = currentMode;
        CountPhotos = countPhotos;
        JustVersion = justVersion;
    }

    public AllResponse Results { get; }
    public Dictionary<string, string> UrlsToTitles { get; }
    public int Hidden { get; }
    public Mode CurrentMode { get; }
    public int CountPhotos { get; }
    public int? JustVersion { get; }

    public enum Mode
    {
        None,
        Brief,
        BriefWithPrompts,
        Full
    }
}