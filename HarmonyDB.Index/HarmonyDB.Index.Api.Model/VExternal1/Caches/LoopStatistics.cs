﻿namespace HarmonyDB.Index.Api.Model.VExternal1.Caches;

public class LoopStatistics
{
    public required string Progression { get; init; }

    public required int Length { get; init; }

    public required int TotalSongs { get; init; }

    public required int TotalSuccessions { get; init; }

    public required int TotalOccurrences { get; init; }

    public required bool IsCompound { get; init; }

    public required IReadOnlyList<byte> RootsStatistics { get; init; }
}