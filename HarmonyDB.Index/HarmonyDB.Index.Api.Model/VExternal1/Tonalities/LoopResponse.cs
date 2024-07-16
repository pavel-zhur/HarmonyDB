namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public class LoopResponse
{
    public required Loop Loop { get; init; }

    public required List<LinkStatistics> LinkStatistics { get; init; }
}