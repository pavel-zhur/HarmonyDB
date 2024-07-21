namespace HarmonyDB.Index.Analysis.Models.Index.Enums;

public enum BlocksExtractionLogic
{
    Loops,
    ReplaceWithSelfJumps,
    ReplaceWithSelfMultiJumps,
    LoopsAndMultiJumps,
    All,
}