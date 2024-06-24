namespace HarmonyDB.Index.BusinessLogic.Models;

public class LoopStatistics
{
    public required string Progression { get; init; }

    public required int Length { get; init; }

    public required int TotalSongs { get; init; }

    public required int TotalSuccessions { get; init; }

    public required int TotalOccurrences { get; init; }
}