using System.Text;
using System.Text.Json.Serialization;
using System.Web;

namespace HarmonyDB.Common.Representations.OneShelf;

public class NodeNote : NodeBase
{
    public NodeNote(Note note)
    {
        Note = note;
    }

    public Note Note { get; }

    /// <summary>
    /// How many sharps (positive) or flats (negative) the note was written with, when it was read from a chord:
    /// E# is E with one sharp, Bbb is B with two flats. See <see cref="RepresentationSettings.ShowNotesAsWritten"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Accidentals { get; init; }

    public string Representation(RepresentationSettings representationSettings)
    {
        // Not transposed and with no sharps or flats asked for, a note written with two or more signs is shown as written, as it always was;
        // with one sign, when the setting says so.
        if (Accidentals is { } accidentals
            && (representationSettings.ShowNotesAsWritten || Math.Abs(accidentals) > 1)
            && representationSettings is { Transpose: 0, Alteration: null, RelativeTo: null, TestOnlyX: false })
            return Note.Written(Note.Value, accidentals);

        return Note.Representation(representationSettings);
    }

    /// <summary>
    /// The length of the note as written in the source text.
    /// </summary>
    public int Length() => 1 + Math.Abs(Accidentals ?? (Note.Alteration.HasValue ? 1 : 0));

    public int EstimatedLength(RepresentationSettings representationSettings) => Representation(representationSettings).Length;

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
        builder.Append(Representation(representationSettings));
    }

    public override NodeBase ShallowClone() => new NodeNote(Note) { Accidentals = Accidentals };
}
