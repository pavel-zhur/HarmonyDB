using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using OneShelf.Common;

namespace HarmonyDB.Common.Representations.OneShelf;

public class NodeChord : NodeCollectionBase
{
    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings,
        TraversalContext traversalContext)
    {
        var me = traversalContext.SiblingChords?.IndexOf(this);
        if (me == -1) throw new("Could not have happened.");

        var index = traversalContext.ChordIndex++;

        if (me > 0 && representationSettings.Simplification > SimplificationMode.None && traversalContext.SiblingChords![me.Value - 1].Representation(representationSettings) ==
            Representation(representationSettings))
        {
            builder.Append(string.Join(string.Empty, Enumerable.Repeat(" ", Representation(representationSettings).Length)));
            return;
        }

        builder.Append($"<b class='chord' chord-index='{index}' chord-custom='{HttpUtility.HtmlAttributeEncode(representationSettings.CustomAttributes.GetValueOrDefault(index))}' chord-data='");

        AppendHtmlAttributeTo(builder, representationSettings);

        builder.Append("'>");

        traversalContext.IsInsideChord = true;
        base.AppendHtmlTo(builder, representationSettings, traversalContext);
        traversalContext.IsInsideChord = false;

        builder.Append("</b>");
    }

    internal override void AppendChordsTo(List<string> destination, RepresentationSettings representationSettings)
    {
        var builder = new StringBuilder();
        AppendHtmlAttributeTo(builder, representationSettings);
        var chord = builder.ToString();
        if (chord.Contains('\r') || chord.Contains('\n'))
            throw new("The chord may not contain the newline characters.");
        destination.Add(chord);
    }

    internal override void AppendLyricsTo(StringBuilder builder)
    {
    }

    internal override void AppendOneShelfTo(StringBuilder builder, RepresentationSettings representationSettings,
        bool isBold, ref bool owesWhitespace, bool alreadyChordLine)
    {
        if (alreadyChordLine)
        {
            base.AppendOneShelfTo(builder, representationSettings, isBold, ref owesWhitespace, alreadyChordLine);
            return;
        }

        if (builder.Length > 0 && builder[^1] == ' ')
        {
            builder[^1] = '<';
        }
        else
        {
            builder.Append("<");
        }
        
        base.AppendOneShelfTo(builder, representationSettings, isBold, ref owesWhitespace, alreadyChordLine);
        builder.Append(">");
        owesWhitespace = true;
    }

    public string Representation(RepresentationSettings representationSettings) => string.Join(
        string.Empty,
        Children.Select(x => x.AsNode() switch
        {
            NodeNote nodeNote => nodeNote.Representation(representationSettings),
            NodeText nodeText => nodeText.Text,
            _ => throw new ArgumentOutOfRangeException(nameof(x)),
        }));

    public override void Fix1_ExtractChordNotes(RepresentationSettings representationSettings,
        (NodeCollectionBase parent, int myIndex)? parent2 = null)
    {
        var chord = string.Join(string.Empty, Children.Select(x => x.AsNode()).Cast<NodeText>().Select(x => x.Text));

        if (chord.Contains("\n")) throw new("A chord may not contain a new line.");

        if (chord == string.Empty) throw new("A chord may not be empty.");

        var (parent, myIndex) = parent2!.Value;
        
        // 6773577253408358652_-8081625970822610332 fix
        // todo: may be other impossible chords. run analysis.
        if (chord.StartsWith("-") && chord.All(x => x == '-'))
        {
            parent.Children[myIndex] = new NodeText(chord).AsChild();
            return;
        }

        if (chord.StartsWith('-'))
        {
            throw new($"A chord may not start with a -. Text: {chord}");
        }
        
        if (chord.StartsWith('|') || chord.StartsWith('/'))
        {
            if (chord.Length > 1 && chord[1] is '|' or '/')
                throw new("Multiple vertical lines or slahses at the beginning of the chord are not supported.");

            if (chord.Length > 1)
            {
                parent.Children.Insert(myIndex, new NodeText(chord.Substring(0, 1)).AsChild());

                var firstNonEmptyChildIndex = Children.Select(x => x.AsNode()).Cast<NodeText>().WithIndices().First(x => x.x.Text.Length > 0).i;
                Children[firstNonEmptyChildIndex] = new NodeText(chord.Substring(1)).AsChild();
            }
            else
            {
                parent.Children[myIndex] = new NodeText(chord).AsChild();
            }

            return; // gone or prepended and will be processed again
        }

        var postfix = string.Empty;
        if (chord.Contains("--"))
        {
            var index = chord.IndexOf('-');
            postfix = chord.Substring(index);
            chord = chord.Substring(0, index);
        }
        else if (chord.EndsWith('-') && !char.IsDigit(chord[^2]))
        {
            postfix = chord.Substring(chord.Length - 1);
            chord = chord.Substring(0, chord.Length - 1);
        }

        var parts = chord.Split('/');
        if (representationSettings.Simplification.HasFlag(SimplificationMode.RemoveBass))
        {
            postfix += string.Join(string.Empty, Enumerable.Repeat(' ', parts.Length - 1 + parts.Skip(1).Sum(x => x.Length)));
            parts = parts.Take(1).ToArray();
        }

        var newChildren = new List<NodeChild>();

        foreach (var (partOriginal, isFirst) in parts.WithIsFirst())
        {
            if (!isFirst)
            {
                newChildren.Add(new NodeText("/").AsChild());
            }

            var part = partOriginal;
            if (isFirst)
            {
                var oldLength = part.Length;
                part = Simplify(part, representationSettings.Simplification);
                postfix += string.Join(string.Empty, Enumerable.Repeat(' ', oldLength - part.Length));
            }

            if (part == string.Empty) continue;

            if (!Note.CharactersToNotes.TryGetValue(part[0], out var start))
            {
                newChildren.Add(new NodeText(part).AsChild());
                continue;
            }

            var modified = false;
            if (part.Length > 1 && part[1] == '#')
            {
                start = start.Sharp();
                modified = true;
            }

            if (part.Length > 1 && part[1] == 'b')
            {
                start = start.Flat();
                modified = true;
            }

            newChildren.Add(new NodeNote(start).AsChild());

            if (part.Length > (modified ? 2 : 1))
            {
                newChildren.Add(new NodeText(part.Substring(modified ? 2 : 1)).AsChild());
            }
        }

        Children.Clear();
        Children.AddRange(newChildren);

        if (postfix.Length > 0)
        {
            parent.Children.Insert(myIndex + 1, new NodeText(postfix).AsChild());
        }
    }

    private static string Simplify(string part, SimplificationMode simplificationMode)
    {
        if (simplificationMode.HasFlag(SimplificationMode.Remove9AndMore))
        {
            part = Regex.Replace(
                part,
                "^(?<take>m|maj|mmaj)?[b\\-\\+\\#]?(9|11|13)(b\\-\\+\\#)?$",
                "${take}7");

            part = Regex.Replace(
                part,
                "^(9|11|13)(?<retain>sus|add|dim|aug|lyd)",
                "7${retain}");

            for (var i = 0; i < 3; i++)
            {
                part = Regex.Replace(part,
                    "add(9|11|13)[\\-\\+]?$|\\((9|11|13)\\-\\)|\\((b|add|add\\#|addb|add\\-|add\\+|\\-|\\#)?(9|11|13)\\)|^M(9|11|13)$|(add|addb|add\\-|add\\#|add\\+|b|\\#|\\-|\\+)?(9|11|13)[\\+\\-b]?$",
                    string.Empty);
            }

            part = part.Replace("(b9,b13)", string.Empty);
        }

        if (simplificationMode.HasFlag(SimplificationMode.Remove6))
        {
            part = new[]
            {
                "add6", "(add6)", "addb6", "(addb6)", "addb6", "(add6b)", "(6b)", "(6b)", "b6", "6b", "(6)", "-6", "(-6)"
            }.Aggregate(part,
                (current, search) => current.EndsWith(search)
                    ? current.Substring(0, current.Length - search.Length)
                    : current);

            part = part.Replace("mmin6", "m").Replace("min6", "m");

            part = part.Replace("6", string.Empty);
        }

        if (simplificationMode.HasFlag(SimplificationMode.RemoveSus))
        {
            part = new[]
            {
                "sus4", "sus2", "sus", "(sus4)", "(sus2)", "(sus)", 
            }.Aggregate(part,
                (current, search) => current.Contains(search, StringComparison.InvariantCultureIgnoreCase)
                    ? current.Replace(search, current.Contains("5") || current.StartsWith("m", StringComparison.InvariantCultureIgnoreCase) && !current.StartsWith("ma") ? string.Empty : "5", StringComparison.InvariantCultureIgnoreCase)
                    : current);
        }

        if (simplificationMode.HasFlag(SimplificationMode.Remove9AndMore))
        {
            var any = false;
            for (var i = 0; i < 3; i++)
            {
                foreach (var search in new[] { "9", "11", "13" })
                {
                    var postfix = $"add{search}#";
                    if (part.EndsWith(postfix))
                    {
                        part = part.Replace(postfix, string.Empty);
                        any = true;
                    }

                    postfix = $"(add{search}#)";
                    if (part.EndsWith(postfix))
                    {
                        part = part.Replace(postfix, string.Empty);
                        any = true;
                    }

                    postfix = $"add{search}";
                    if (part.Contains(postfix))
                    {
                        part = part.Replace(postfix, string.Empty);
                        any = true;
                    }

                    postfix = $"(add{search})";
                    if (part.Contains(postfix))
                    {
                        part = part.Replace(postfix, string.Empty);
                        any = true;
                    }

                    postfix = $"(maj{search})";
                    if (part.Contains(postfix))
                    {
                        part = part.Replace(postfix, "maj7");
                        any = true;
                    }

                    postfix = $"maj{search}";
                    if (part.Contains(postfix))
                    {
                        part = part.Replace(postfix, "maj7");
                        any = true;
                    }
                }

                if (!any) break;
            }

            part = part.Replace("9", part.Contains("7") ? string.Empty : "7");
            part = part.Replace("11", part.Contains("7") ? string.Empty : "7");
            part = part.Replace("13", part.Contains("7") ? string.Empty : "7");
        }

        if (simplificationMode.HasFlag(SimplificationMode.Remove7))
        {
            part = Regex.Replace(
                part,
                "^7\\+$|^7M|\\(maj7\\)|maj7|major7|^\\+7|7\\+$|7\\#$|\\+7$|^7$|\\((maj|\\#)?7[\\+\\#]?\\)",
                string.Empty,
                RegexOptions.IgnoreCase)
                .Replace("7", string.Empty);
        }

        return part;
    }

    public override NodeBase ShallowClone() => new NodeChord();
}