using System.Text;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.TextGraphics;

public class Text
{
    private List<(string? css, string text)> _pieces = new();
    
    public const string CssTextLightGray = "text-lightgray";
    public const string CssTextLightYellow = "text-lightyellow";
    public const string CssTextRed = "text-danger";

    public void Append(string? css, string text)
    {
        if (_pieces.Any() && _pieces[^1] is var last && last.css == css)
        {
            _pieces[^1] = last with { text = last.text + text };
        }
        else
        {
            _pieces.Add((css, text));
        }
    }

    public void Append(Text text)
        => text._pieces.ForEach(_pieces.Add);

    public static Text Empty => new();
    
    public static Text NewLine
    {
        get
        {
            var result = new Text();
            result.Append(null, Environment.NewLine);
            return result;
        }
    }

    public static Text Join(Text separator, IEnumerable<Text> lines)
    {
        var result = Empty;
        foreach (var (x, isFirst) in lines.WithIsFirst())
        {
            if (!isFirst)
            {
                result.Append(separator);
            }
            
            result.Append(x);
        }

        return result;
    }
    
    public string AsHtml()
    {
        _pieces = _pieces.ToChunks(x => x.css).Select(c => (c.criterium, string.Join(string.Empty, c.chunk.Select(x => x.text)))).ToList();
        
        var stringBuilder = new StringBuilder();
        foreach (var (css, text) in _pieces)
        {
            stringBuilder.Append($"<span class=\"{css}\">{text}</span>");
        }

        return stringBuilder.ToString();
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();
        foreach (var (_, text) in _pieces)
        {
            stringBuilder.Append(text);
        }

        return stringBuilder.ToString();
    }
}