namespace HarmonyDB.Index.BusinessLogic.Models;

public class TonalitiesIndexSong
{
    public required string ExternalId { get; init; }

    public required IReadOnlyList<float> Probabilities { get; init; }

    public required bool DespiteStable { get; init; }
}