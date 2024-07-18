namespace HarmonyDB.Index.Analysis.Models.Index;

public enum BlocksExtractionLogic
{
    Loops,
    ReplaceWithSelfJumps,
    ReplaceWithSelfMultiJumps,
    All,
}