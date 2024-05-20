using System.Text;
using System.Text.Json.Serialization;

namespace HarmonyDB.Common.Representations.OneShelf;

public class NodeHtml : NodeCollectionBase
{
    public const string PreVariableWidthClass = "pre-variable-width";

    public required string Name { get; init; }

    public string? Classes { get; init; }

    [JsonIgnore]
    public bool IsBlock { get; set; }

    [JsonIgnore]
    public bool IsVariableWidth => Classes == PreVariableWidthClass;

    public IEnumerable<NodeChord> GetAllChords(RepresentationSettings representationSettings)
    {
        return CreateFinal(representationSettings).TraverseAllChords();
    }

    public string AsLyrics(bool trim = true)
    {
        var final = CreateFinal(new());
        var builder = new StringBuilder();
        final.AppendLyricsTo(builder);
        if (trim)
        {
            return string.Join(Environment.NewLine,
                builder.ToString().Split(new[] { '\r', '\n' },
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        return builder.ToString();
    }

    public string AsHtml(RepresentationSettings representationSettings)
    {
        var final = CreateFinal(representationSettings);
        var builder = new StringBuilder();
        final.AppendHtmlTo(builder, representationSettings, new());
        return builder.ToString();
    }

    public List<string> AsChords(RepresentationSettings representationSettings)
    {
        var final = CreateFinal(representationSettings, false, false);
        var destination = new List<string>();
        final.AppendChordsTo(destination, representationSettings);
        return destination;
    }

    public string AsOneShelf(RepresentationSettings representationSettings)
    {
        var final = CreateFinal(representationSettings, false);
        var builder = new StringBuilder();

        foreach (var line in final.Children.Select(c => c.NodeLine!))
        {
            var owesWhitespace = false;
            if (line.TraverseAll().Skip(1).All(x =>
                    x.element is NodeText text && (string.IsNullOrWhiteSpace(text.Text) || x.parentsReverse.Any(x => x is NodeChord))
                    || x.element is NodeChord
                    || x.element is NodeNote)
                && line.TraverseAll().Any(x => x.element is NodeChord))
            {
                builder.Append(">");
                owesWhitespace = true;
                line.AppendOneShelfTo(builder, representationSettings, false, ref owesWhitespace, true);
            }
            else if (line.TraverseAll().Skip(1).All(x => x.element is not (NodeChord or NodeLine))
                     && line.Children is [{ NodeHtml: { Name: "b" } or { Name: "div", Classes: "bold-div" } }])
            {
                builder.Append("[");
                owesWhitespace = true;
                foreach (var lineChild in line.Children)
                {
                    lineChild.AsNode().AppendOneShelfTo(builder, representationSettings, false, ref owesWhitespace, false);
                    builder.AppendLine("]");
                }
            }
            else
            {
                line.AppendOneShelfTo(builder, representationSettings, false, ref owesWhitespace, false);
            }
        }
        
        return builder.ToString();
    }

    private NodeHtml CreateFinal(RepresentationSettings representationSettings, bool create5DoubleLines = true, bool create234 = true)
    {
        var final = (NodeHtml)DeepClone();
        final.Fix1_ExtractChordNotes(representationSettings);

        if (create234)
        {
            final.Fix2_ExtractLines();
            final.Fix3_AssignBlockIds();
            final = final.Fix4_NewLines();

            if (create5DoubleLines)
            {
                final.Fix5_CreateDoubleLines(representationSettings);
            }
        }

        return final;
    }

    private NodeHtml Fix4_NewLines()
    {
        var result = (NodeHtml)ShallowClone();

        while (true)
        {
            var (clone, completed) = CloneFixingNewLines();
            var nodeLine = new NodeLine();
            nodeLine.Children.AddRange(clone.Children);
            result.Children.Add(nodeLine.AsChild());

            if (completed) break;
        }

        return result;
    }

    private void Fix5_CreateDoubleLines(RepresentationSettings representationSettings)
    {
        var lines = Children.Select(x => x.AsNode()).Cast<NodeLine>().ToList();
        for (var i = 0; i < lines.Count - 1; i++)
        {
            lines[i].TryInjectTo(lines[i + 1], representationSettings);
        }
    }

    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings,
        TraversalContext traversalContext)
    {
        if (!string.IsNullOrWhiteSpace(Classes))
        {
            builder.Append('<');
            builder.Append(Name);
            builder.Append(" class='");
            builder.Append(Classes);
            builder.Append("'>");
        }
        else
        {
            builder.Append('<');
            builder.Append(Name);
            builder.Append('>');
        }

        base.AppendHtmlTo(builder, representationSettings, traversalContext);

        builder.Append("</");
        builder.Append(Name);
        builder.Append('>');
    }

    public override NodeBase ShallowClone() => new NodeHtml
    {
        Name = Name,
        Classes = Classes,
    };

    public int? GetChordsCount()
    {
	    return TraverseAll().Count(x => x.element is NodeChord);
    }

    public int? GetNodesCount()
    {
	    return TraverseAll().Count();
    }
}