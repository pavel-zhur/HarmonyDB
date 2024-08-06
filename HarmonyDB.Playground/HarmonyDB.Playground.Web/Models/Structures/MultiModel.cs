using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Playground.Web.Models.Structures;

public record MultiModel : PolyBlocksExtractionParameters
{
    public List<string> ExternalIds { get; set; } = new();
    
    public int? ToRemove { get; set; }

    public bool IncludeTrace { get; init; }

    public List<BlockType> BlockTypes { get; init; } =
    [
        BlockType.Sequence,
        BlockType.SequenceStart,
        BlockType.SequenceEnd,
        BlockType.Loop,
        BlockType.RoundRobin,
    ];
}