namespace HarmonyDB.Index.BusinessLogic.Models;

public class TonalitiesIndexLoop
{
    public required string Progression { get; init; }

    public required string Normalized { get; init; }

    public required IReadOnlyList<float> Probabilities { get; init; }

    public required IReadOnlyDictionary<string, List<SongLoopLink>> Songs { get; init; }

    public required int TotalOccurrences { get; init; }

    public required int TotalSuccessions { get; init; }
}