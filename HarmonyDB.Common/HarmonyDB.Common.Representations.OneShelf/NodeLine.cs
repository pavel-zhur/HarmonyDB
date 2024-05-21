using System.Text;
using OneShelf.Common;

namespace HarmonyDB.Common.Representations.OneShelf;

public class NodeLine : NodeCollectionBase
{
    private bool _isDouble;
    private bool _isZero;

    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings,
        TraversalContext traversalContext)
    {
        if (_isZero) return;

        if (_isDouble)
        {
            builder.Append("<div class='chords-double-line'>");
        }

        builder.Append($"<span class='chords-line {(_isDouble ? "chords-double-line" : null)}'>");

        traversalContext.SiblingChords = TraverseAll().Select(x => x.element).OfType<NodeChord>().ToList();
        base.AppendHtmlTo(builder, representationSettings, traversalContext);
        traversalContext.SiblingChords = null;

        builder.Append("</span>");

        if (_isDouble)
        {
            builder.Append("</div>");
        }
        else
        {
            builder.Append("\n");
        }
    }

    internal override void AppendLyricsTo(StringBuilder builder)
    {
        if (_isZero) return;
        base.AppendLyricsTo(builder);
        builder.AppendLine();
    }

    internal override void AppendOneShelfTo(StringBuilder builder, RepresentationSettings representationSettings,
        bool isBold, ref bool owesWhitespace, bool alreadyChordLine)
    {
        base.AppendOneShelfTo(builder, representationSettings, isBold, ref owesWhitespace, alreadyChordLine);
        builder.AppendLine();
        owesWhitespace = false;
    }

    public override NodeBase ShallowClone() => new NodeLine();

    public override (NodeCollectionBase clone, bool completed) CloneFixingNewLines()
    {
        throw new("This operation will not be called on this type.");
    }

    public void TryInjectTo(NodeLine next, RepresentationSettings representationSettings)
    {
        var currentLineHasTextOutsideChords = TraverseAll().Any(x =>
            x.element is NodeText nodeText && !string.IsNullOrWhiteSpace(nodeText.Text) &&
            !x.parentsReverse.Any(p => p is NodeChord) && nodeText.Text.Any(c => !char.IsWhiteSpace(c) && !"()*/\\!-|'_><".Contains(c)));

        if (currentLineHasTextOutsideChords) return;

        var currentLineHasChords = TraverseAll().Any(x => x.element is NodeChord);

        if (!currentLineHasChords) return;

        var nextLineHasChords = next.TraverseAll().Any(x => x.element is NodeChord);

        if (nextLineHasChords) return;

        var nextLineHasNonEmptyText = next.TraverseAll()
            .Any(x => x.element is NodeText nodeText && !string.IsNullOrWhiteSpace(nodeText.Text));

        if (!nextLineHasNonEmptyText) return;

        var differentBlockIds = TraverseAll()
            .Where(x => x.element != this)
            .Select(x => x.element.BlockId)
            .Concat(next.TraverseAll()
                .Where(x => x.element != next)
                .Select(x => x.element.BlockId))
            .Distinct()
            .Count() > 1;
        
        if (differentBlockIds) return;

        var position = 0;
        var flies = new List<(int position, NodeBase node, int length)>();
        var fix = representationSettings.IsVariableWidth ? .66 : 1;
        foreach (var child in TraverseAll())
        {
            switch (child)
            {
                case (NodeText nodeText, { } parents):
                    if (parents.OfType<NodeChord>().Any()) break;

                    if (!string.IsNullOrWhiteSpace(nodeText.Text))
                    {
                        var trimmed = nodeText.Text.Trim();
                        var trimmedIndex = nodeText.Text.IndexOf(trimmed, StringComparison.Ordinal);
                        if (trimmedIndex == -1) throw new("Could not have happened.");
                        
                        position += (int)(trimmedIndex * fix);
                        flies.Add((position, new NodeText(trimmed), trimmed.Length));
                        position += (int)((nodeText.Text.Length - trimmedIndex) * fix);
                    }
                    else
                    {
                        position += (int)(nodeText.Text.Length * fix);
                    }

                    break;
                case (NodeChord nodeChord, _):
                    var length = nodeChord.TraverseAll().Sum(x => x.element switch
                    {
                        NodeText text => text.Text.Length,
                        NodeNote note => note.Note.Length(),
                        _ => 0
                    });
                    var estimatedLength = nodeChord.TraverseAll().Sum(x => x.element switch
                    {
                        NodeText text => text.Text.Length,
                        NodeNote note => (int)(note.Note.EstimatedLength(representationSettings) / fix) + (representationSettings.IsVariableWidth ? 2 : 0),
                        _ => 0
                    });
                    flies.Add((position, nodeChord, estimatedLength));
                    position += length;
                    break;
            }
        }

        if (!flies.Any()) return;

        flies = flies.OrderBy(x => x.position).ToList();

        position = 0;
        for (var i = 0; i < flies.Count; i++)
        {
            var fly = flies[i];
            if (fly.position < position)
            {
                flies[i] = (position, fly.node, fly.length);
                fly = flies[i];
            }

            position = fly.position + fly.length + 1;
        }

        var line1Length = flies
            .Select(x => x.position + x.length)
            .Max();
        var line2Length = next.TraverseAll().Sum(x => x.element is NodeText text ? text.Text.Length : 0);

        if (line1Length > line2Length)
        {
            next.Children.Add(new NodeText(string.Join(string.Empty, Enumerable.Repeat(" ", line1Length - line2Length + 1))).AsChild());
        }

        position = 0;
        foreach (var child in next.TraverseAll().ToList())
        {
            if (child.element is not NodeText text) continue;

            if (text.Text.Length == 0) continue;

            var start = position;
            var end = position + text.Text.Length;
            position = end;

            var matching = flies
                .Where(x => x.position >= start && x.position < end)
                .Select(x => (x.node, x.position))
                .ToList();

            if (!matching.Any()) continue;

            var parent = child.parentsReverse.First();
            var currentText = text;
            var currentPosition = start;

            foreach (var (node, position2) in matching)
            {
                var position2Relative = position2 - currentPosition;
                var currentTextIndex = parent.Children.IndexOf(currentText.AsChild());
                var part1 = currentText.Text.Substring(0, position2Relative);
                var part2 = currentText.Text.Substring(position2Relative);
                parent.Children[currentTextIndex] = new NodeText(part1).AsChild();
                currentText = new(part2);
                currentPosition += part1.Length;
                parent.Children.InsertRange(currentTextIndex + 1,
                    new NodeHtml
                        {
                            Name = "span",
                            Classes = "flying",
                            Children =
                            {
                                new NodeHtml
                                {
                                    Name = "span",
                                    Classes = "flying2",
                                    Children =
                                    {
                                        node.AsChild(),
                                    }
                                }.AsChild(),
                            }
                        }.AsChild()
                        .Once()
                        .Append(currentText.AsChild()));
            }
        }

        Children.Clear();
        next._isDouble = true;
        _isZero = true;
    }
}