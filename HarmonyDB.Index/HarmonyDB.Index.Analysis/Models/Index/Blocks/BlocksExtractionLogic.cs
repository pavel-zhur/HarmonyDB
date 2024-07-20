namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public enum BlocksExtractionLogic
{
    Loops,
    ReplaceWithSelfJumps,
    ReplaceWithSelfMultiJumps,
    All,
}