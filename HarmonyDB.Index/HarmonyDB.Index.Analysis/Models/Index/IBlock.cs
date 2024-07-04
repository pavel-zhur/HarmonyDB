namespace HarmonyDB.Index.Analysis.Models.Index;

public interface IBlock
{
    int StartIndex { get; }

    int EndIndex { get; }
}