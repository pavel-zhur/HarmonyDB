using HarmonyDB.Index.Analysis.Models.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.CompactV1;

public class CompactHarmonyMovementsSequence : ISearchableHarmonyMovementsSequence
{
    public required int FirstMovementFromIndex { get; init; }

    public required int FirstRoot { get; init; }

    public required ReadOnlyMemory<CompactHarmonyMovement> Movements { get; init; }

    int ISearchableHarmonyMovementsSequence.MovementsCount => Movements.Length;

    CompactHarmonyMovement ISearchableHarmonyMovementsSequence.GetMovement(int index) => Movements.Span[index];
}