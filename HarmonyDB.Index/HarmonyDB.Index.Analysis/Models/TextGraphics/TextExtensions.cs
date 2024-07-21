namespace HarmonyDB.Index.Analysis.Models.TextGraphics;

public static class TextExtensions
{
    public static Text AsText(this string text, string? css = null)
    {
        var result = new Text();
        result.Append(css, text);
        return result;
    }
    
    public static Text AsText(this char text, string? css = null)
    {
        var result = new Text();
        result.Append(css, text.ToString());
        return result;
    }
}