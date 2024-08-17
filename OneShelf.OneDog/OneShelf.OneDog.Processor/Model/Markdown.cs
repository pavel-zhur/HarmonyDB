using System.Diagnostics.CodeAnalysis;
using System.Text;
using OneShelf.OneDog.Processor.Helpers;

namespace OneShelf.OneDog.Processor.Model;

public class Markdown
{
    private readonly StringBuilder _builder = new();

    public static Markdown Empty => new();

    public static Markdown UnsafeFromMarkdownString(string markup)
    {
        var result = new Markdown();
        result._builder.Append(markup);
        return result;
    }

    public static implicit operator Markdown(string? line) => line?.ToMarkdown() ?? Empty;

    public static Markdown operator +(Markdown? markup1, Markdown? markup2)
    {
        var result = new Markdown();
        result._builder.Append(markup1?._builder);
        result._builder.Append(markup2?._builder);
        return result;
    }
    
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] Markdown? markup) => markup == null || string.IsNullOrWhiteSpace(markup._builder.ToString());

    public override string ToString() => _builder.ToString();

    public void Append(Markdown? markup)
    {
        _builder.Append(markup);
    }

    public void AppendLine()
    {
        _builder.AppendLine();
    }

    public void AppendLine(Markdown markdown)
    {
        _builder.AppendLine(markdown.ToString());
    }

    public bool EndsWith(Markdown markdown)
    {
        var ending = markdown.ToString();
        return _builder.Length >= ending.Length && _builder.ToString(_builder.Length - ending.Length, ending.Length) == ending;
    }

    public static Markdown Join(Markdown separator, IEnumerable<Markdown> items)
    {
        var result = new Markdown();
        var first = true;
        foreach (var item in items)
        {
            if (!first)
            {
                result.Append(separator);
            }

            first = false;
            result.Append(item);
        }

        return result;
    }
}