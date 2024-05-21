using System.Text;
using System.Text.Json.Serialization;

namespace HarmonyDB.Common.Representations.OneShelf;

public abstract class NodeBase
{
    [JsonIgnore]
    public double? BlockId { get; set; }

    internal abstract void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings, TraversalContext traversalContext);

    internal abstract void AppendChordsTo(List<string> destination, RepresentationSettings representationSettings);
    
    internal abstract void AppendLyricsTo(StringBuilder builder);

    internal abstract void AppendHtmlAttributeTo(StringBuilder builder, RepresentationSettings representationSettings);

    internal abstract void AppendOneShelfTo(StringBuilder builder, RepresentationSettings representationSettings,
        bool isBold, ref bool owesWhitespace, bool alreadyChordLine);

    public virtual void Fix1_ExtractChordNotes(RepresentationSettings representationSettings,
        (NodeCollectionBase parent, int myIndex)? parent = null)
    {
    }

    public virtual void Fix2_ExtractLines()
    {
    }

    public abstract NodeBase ShallowClone();

    public NodeChild AsChild() => new(this);
}