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

    /// <summary>
    /// The moment of this node, in seconds: its time, or its place in the sheet's bars; none (null) when it has neither,
    /// or when its place is not in the bars (or the sheet has none). Every time the rendering reads is read here.
    /// </summary>
    internal virtual double? Moment(SheetTimes? times) => null;

    /// <summary>
    /// The moment of a node that has a time or a place (<see cref="Moment"/>). A node with both has no time: the two may disagree.
    /// </summary>
    internal static double? Moment(double? time, SheetPlace? place, SheetTimes? times)
    {
        if (time.HasValue && place != null) return null;

        return time ?? (place != null ? times?.Moment(place) : null);
    }

    public NodeChild AsChild() => new(this);
}