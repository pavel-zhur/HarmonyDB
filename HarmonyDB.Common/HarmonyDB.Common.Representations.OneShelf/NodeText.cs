using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace HarmonyDB.Common.Representations.OneShelf;

public class NodeText : NodeBase
{
    public NodeText(string text)
    {
        Text = text.Replace("\r", "");
    }

    public string Text { get; }
    
    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings,
        TraversalContext traversalContext)
    {
        builder.Append(traversalContext.IsInsideChord ? Beautify(Text) : HttpUtility.HtmlEncode(Text));
    }

    private string Beautify(string text) =>
        Regex.Replace(text,
            "(?<up>(b|#|-|\\+)(5|6|7|9|11|13)|(5|6|7|9|11|13)(b|#|-|\\+)(?!\\d)|(\\([^\\(\\)]+\\)){2,})|\\((?<up>[^\\(\\)]+)\\)|(?<up>[5oO]$)|(?<down>[12346789]+?)",
            m =>
            {
                if (m.Groups["up"].Success)
                {
                    return $"<sup>{HttpUtility.HtmlEncode(m.Groups["up"].Value)}</sup>";
                }

                if (m.Groups["down"].Success)
                {
                    return $"<sub>{HttpUtility.HtmlEncode(m.Groups["down"].Value)}</sub>";
                }

                return HttpUtility.HtmlEncode(m.Value);
            },
            RegexOptions.Compiled);

    internal override void AppendChordsTo(List<string> destination, RepresentationSettings representationSettings)
    {
    }

    internal override void AppendLyricsTo(StringBuilder builder)
    {
        builder.Append(Text);
    }

    internal override void AppendHtmlAttributeTo(StringBuilder builder, RepresentationSettings representationSettings)
    {
        builder.Append(HttpUtility.HtmlAttributeEncode(Text));
    }

    internal override void AppendOneShelfTo(StringBuilder builder, RepresentationSettings representationSettings,
        bool isBold, ref bool owesWhitespace, bool alreadyChordLine)
    {
        if (owesWhitespace && Text.StartsWith(' ') && Text.Length > 1)
        {
            builder.Append(Text.Substring(1));
            owesWhitespace = false;
        }
        else
        {
            builder.Append(Text);
        }
    }

    public override NodeBase ShallowClone() => new NodeText(Text);
}