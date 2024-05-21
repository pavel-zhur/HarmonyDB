using System.Text;
using System.Web;

namespace HarmonyDB.Common.Representations.OneShelf;

public class NodeNote : NodeBase
{
    public NodeNote(Note note)
    {
        Note = note;
    }

    public Note Note { get; }
    
    public string Representation(RepresentationSettings representationSettings)
    {
        return Note.Representation(representationSettings);
    }

    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings,
        TraversalContext traversalContext)
    {
        builder.Append(HttpUtility.HtmlEncode(Representation(representationSettings)));
    }

    internal override void AppendChordsTo(List<string> destination, RepresentationSettings representationSettings)
    {
        throw new InvalidOperationException();
    }

    internal override void AppendLyricsTo(StringBuilder builder)
    {
        throw new InvalidOperationException();
    }

    internal override void AppendHtmlAttributeTo(StringBuilder builder, RepresentationSettings representationSettings)
    {
        builder.Append(HttpUtility.HtmlAttributeEncode($"[!{Note.Tone(representationSettings)}{(representationSettings.Alteration ?? Note.Alteration) switch {
            NoteAlteration.Flat => "b", 
            NoteAlteration.Sharp => "#", 
            null => null, 
            _ => throw new ArgumentOutOfRangeException() }}!]"));
    }

    internal override void AppendOneShelfTo(StringBuilder builder, RepresentationSettings representationSettings,
        bool isBold, ref bool owesWhitespace, bool alreadyChordLine)
    {
        builder.Append(Note.Representation(representationSettings));
    }

    public override NodeBase ShallowClone() => new NodeNote(Note);
}