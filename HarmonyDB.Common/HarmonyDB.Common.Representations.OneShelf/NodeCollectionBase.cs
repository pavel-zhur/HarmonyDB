using System.Text;
using OneShelf.Common;

namespace HarmonyDB.Common.Representations.OneShelf;

public abstract class NodeCollectionBase : NodeBase
{
    public List<NodeChild> Children { get; init; } = new();

    public virtual IEnumerable<NodeChord> TraverseAllChords() => Children.SelectMany(x =>
        x.AsNode() is NodeChord chord ? chord.Once() :
        x.AsNode() is NodeCollectionBase collection ? collection.TraverseAllChords() : Enumerable.Empty<NodeChord>());

    public IEnumerable<(NodeBase element, IEnumerable<NodeCollectionBase> parentsReverse)> TraverseAll(IEnumerable<NodeCollectionBase>? parents = null)
    {
        parents ??= Enumerable.Empty<NodeCollectionBase>();

        return Children.SelectMany(x =>
                x.AsNode() is NodeCollectionBase collection
                    ? collection.TraverseAll(parents.Prepend(this))
                    : (x.AsNode(), parentsForChild: parents.Prepend(this)).Once())
            .Prepend((this, parents));
    }

    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings,
        TraversalContext traversalContext)
    {
        Children.ForEach(x => x.AsNode().AppendHtmlTo(builder, representationSettings, traversalContext));
    }

    internal override void AppendChordsTo(List<string> destination, RepresentationSettings representationSettings)
    {
        Children.ForEach(x => x.AsNode().AppendChordsTo(destination, representationSettings));
    }

    internal override void AppendLyricsTo(StringBuilder builder)
    {
        Children.ForEach(x => x.AsNode().AppendLyricsTo(builder));
    }

    internal override void AppendHtmlAttributeTo(StringBuilder builder, RepresentationSettings representationSettings)
    {
        Children.ForEach(x => x.AsNode().AppendHtmlAttributeTo(builder, representationSettings));
    }

    internal override void AppendOneShelfTo(StringBuilder builder, RepresentationSettings representationSettings,
        bool isBold, ref bool owesWhitespace, bool alreadyChordLine)
    {
        foreach (var child in Children)
        {
            child.AsNode().AppendOneShelfTo(builder, representationSettings, isBold, ref owesWhitespace, alreadyChordLine);
        }
    }

    public override void Fix1_ExtractChordNotes(RepresentationSettings representationSettings,
        (NodeCollectionBase parent, int myIndex)? parent = null)
    {
        for (var i = 0; i < Children.Count; i++) // may not convert to foreach because the collection is modified inside the loop
        {
            var node = Children[i];
            node.AsNode().Fix1_ExtractChordNotes(representationSettings, (this, i));
        }
    }

    public override void Fix2_ExtractLines()
    {
        var newChildren = new List<NodeChild>();
        foreach (var child in Children)
        {
            if (child.AsNode() is NodeText nodeText)
            {
                var split = nodeText.Text.Split('\n');
                newChildren.AddRange(split.SelectMany((x, i) => i == 0 ? new NodeText(x).Once() : new NodeLine().Once().Cast<NodeBase>().Append(new NodeText(x))).Select(x => x.AsChild()));
            }
            else
            {
                newChildren.Add(child);
            }
        }

        Children.Clear();
        Children.AddRange(newChildren);
    }

    public void Fix3_AssignBlockIds(double? blockId = null)
    {
        BlockId = blockId;
        foreach (var child in Children)
        {
            if (child.AsNode() is NodeCollectionBase childCollectionBase)
            {
                childCollectionBase.Fix3_AssignBlockIds(child.AsNode() is NodeHtml { IsBlock: true } ? Random.Shared.NextDouble() : blockId);
            }
            else
            {
                child.AsNode().BlockId = blockId;
            }
        }
    }

    public NodeCollectionBase DeepClone()
    {
        var clone = (NodeCollectionBase)ShallowClone();
        foreach (var nodeChild in Children)
        {
            switch (nodeChild.AsNode())
            {
                case NodeCollectionBase nodeCollectionBase:
                    clone.Children.Add(nodeCollectionBase.DeepClone().AsChild());
                    break;
                default:
                    clone.Children.Add(nodeChild.AsNode().ShallowClone().AsChild());
                    break;
            }
        }

        return clone;
    }

    public virtual (NodeCollectionBase clone, bool completed) CloneFixingNewLines()
    {
        var myClone = (NodeCollectionBase)ShallowClone();

        var completedCount = 0;
        var completed = true;
        foreach (var child in Children)
        {
            if (child.AsNode() is NodeLine)
            {
                completedCount++;
                completed = false;
                break;
            }
            else if (child.AsNode() is NodeCollectionBase childCollectionBase)
            {
                var childClone = childCollectionBase.CloneFixingNewLines();
                myClone.Children.Add(childClone.clone.AsChild());
                if (childClone.completed)
                {
                    completedCount++;
                }
                else
                {
                    completed = false;
                    break;
                }
            }
            else if (child.AsNode() is NodeNote or NodeText)
            {
                myClone.Children.Add(child.AsNode().ShallowClone().AsChild());
                completedCount++;
            }
            else 
            {
                throw new ArgumentOutOfRangeException(nameof(child));
            }
        }

        Children.RemoveRange(0, completedCount);
        return (myClone, completed);
    }
}