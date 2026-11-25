using System.Text;
using System.Text.Json.Serialization;
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

    /// <summary>
    /// When the text (a word, the beginning of a line) is heard, in seconds from the start, when known.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Time { get; init; }

    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings,
        TraversalContext traversalContext)
    {
        if (traversalContext.IsInsideChord)
        {
            builder.Append(representationSettings.ChordSuperscripts ? Beautify(Text) : HttpUtility.HtmlEncode(Text));
            return;
        }

        if (Time.HasValue)
        {
            builder.Append($"<span {TimeAttribute.Format(Time.Value)}>");
            builder.Append(HttpUtility.HtmlEncode(Text));
            builder.Append("</span>");
            return;
        }

        builder.Append(HttpUtility.HtmlEncode(Text));
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

    public override NodeBase ShallowClone() => new NodeText(Text) { Time = Time };
}