using System.Text.RegularExpressions;
using System.Text;
using HarmonyDB.Common.Representations.OneShelf;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Sources.Self.Api.Models;

namespace OneShelf.Sources.Self.Api.Services;

public class StructureParser
{
    public NodeHtml ContentToHtml(Collective collective) => ContentToHtml(collective.Contents.Replace("&", "&amp;"));

    public NodeHtml ContentToHtml(string content)
    {
        var pre = new NodeHtml
        {
            Name = "pre",
        };

        foreach (var source in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = source;
            if (Regex.IsMatch(line,
                    "^(\\s*)>(.*)$")) // whole chords line, place other < > with spaces, trim end, and process add chords
            {
                line = Regex.Replace(line, "^(\\s*)>(.*)$", "$1$2")
                    .Replace('>', ' ')
                    .Replace('<', ' ')
                    .TrimEnd();
                line = $"<{line}>";
            }

            
            if (Regex.IsMatch(line, "^(\\s*)\\[([^<>&]*)\\](\\s*)$"))
            {
                line = Regex.Replace(line, "^(\\s*)\\[([^<>&]*)\\](\\s*)$", "$1$2"); // bold block without chords
                if (pre.Children.Any()) pre.Children.Add(new(new NodeText(Environment.NewLine)));
                pre.Children.Add(new NodeHtml
                        {
                            Name = "b",
                            Children = [
                                new NodeText(line).AsChild(),
                            ],
                        }.AsChild());
                continue;
            }

            var currentLine = new List<NodeChild>();
            NodeChord? currentChord = null;
            StringBuilder buffer = new StringBuilder();

            void Close()
            {
                if (currentChord == null) throw new("No current chord.");
                if (buffer.Length > 0)
                {
                    currentChord.Children.Add(new(new NodeText(buffer.ToString())));
                    buffer.Clear();
                    currentLine.Add(currentChord.AsChild());
                }

                currentChord = null;
            }

            void Open()
            {
                if (currentChord != null) throw new("Already open.");
                if (buffer.Length > 0)
                {
                    currentLine.Add(new(new NodeText(buffer.ToString())));
                    buffer.Clear();
                }

                currentChord = new();
            }

            void Write(char c) => buffer.Append(c);

            void WriteString(string s) => buffer.Append(s);

            var inChord = InChord.Out;
            foreach (var c in line)
            {
                switch (c)
                {
                    case '<':
                        switch (inChord)
                        {
                            case InChord.In:
                                Close();
                                Write(c);
                                Open();
                                break;
                            case InChord.SourceInTargetOut:
                                Write(c);
                                break;
                            case InChord.Out:
                                Write(' ');
                                Open();
                                inChord = InChord.In;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        break;
                    case '>':
                        switch (inChord)
                        {
                            case InChord.In:
                                Close();
                                Write(' ');
                                inChord = InChord.Out;
                                break;
                            case InChord.Out:
                            case InChord.SourceInTargetOut:
                                WriteString("&gt;");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        break;
                    case var _ when char.IsWhiteSpace(c):
                        switch (inChord)
                        {
                            case InChord.In:
                                Close();
                                Write(c);
                                inChord = InChord.SourceInTargetOut;
                                break;
                            case InChord.Out:
                                Write(c);
                                break;
                            case InChord.SourceInTargetOut:
                                Write(c);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        break;
                    default:
                        switch (inChord)
                        {
                            case InChord.In:
                            case InChord.Out:
                                Write(c);
                                break;
                            case InChord.SourceInTargetOut:
                                Open();
                                Write(c);
                                inChord = InChord.In;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        break;
                }
            }

            if (inChord == InChord.In)
                Close();

            if (currentChord != null) throw new("The chord is not closed. Could not have happened.");

            var bufferTail = buffer.ToString().TrimEnd();
            if (!string.IsNullOrEmpty(bufferTail))
            {
                currentLine.Add(new(new NodeText(buffer.ToString())));
            }

            if (pre.Children.Any()) pre.Children.Add(new(new NodeText(Environment.NewLine)));
            pre.Children.AddRange(currentLine);
        }

        return pre;
    }
}