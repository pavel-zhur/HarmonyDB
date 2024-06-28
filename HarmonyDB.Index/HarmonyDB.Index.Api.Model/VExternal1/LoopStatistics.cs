namespace HarmonyDB.Index.Api.Model.VExternal1;

public class LoopStatistics
{
    public required string Progression { get; init; }

    public required int Length { get; init; }

    public required int TotalSongs { get; init; }

    public required int TotalSuccessions { get; init; }

    public required int TotalOccurrences { get; init; }

    public required bool IsCompound { get; init; }
}