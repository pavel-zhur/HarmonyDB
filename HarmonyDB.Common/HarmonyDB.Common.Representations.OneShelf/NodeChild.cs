using System.Text.Json.Serialization;

namespace HarmonyDB.Common.Representations.OneShelf;

public record NodeChild
{
    public NodeChild()
    {
    }

    public NodeChild(NodeBase node)
    {
        switch (node)
        {
            case NodeChord nodeChord:
                NodeChord = nodeChord;
                break;
            case NodeHtml nodeHtml:
                NodeHtml = nodeHtml;
                break;
            case NodeLine nodeLine:
                NodeLine = nodeLine;
                break;
            case NodeNote nodeNote:
                NodeNote = nodeNote;
                break;
            case NodeText nodeText:
                NodeText = nodeText;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(node));
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeChord? NodeChord { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeText? NodeText { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeNote? NodeNote { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeHtml? NodeHtml { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeLine? NodeLine { get; init; }

    public NodeBase AsNode() => NodeChord ?? NodeText ?? NodeNote ?? NodeHtml ?? (NodeBase)NodeLine!;
}