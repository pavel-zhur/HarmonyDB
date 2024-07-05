namespace HarmonyDB.Index.BusinessLogic.Models;

public class TonalitiesIndexLoop
{
    public required string Progression { get; init; }

    public required string Normalized { get; init; }

    public required int Length { get; init; }

    public required IReadOnlyList<float> Probabilities { get; init; }

    public required int TotalOccurrences { get; init; }

    public required int TotalSuccessions { get; init; }

    public required int TotalSongs { get; init; }
}