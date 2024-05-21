using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Interfaces;

namespace HarmonyDB.Index.Analysis.Models;

public class HarmonyMovementsSequence : ISearchableHarmonyMovementsSequence
{
    public required int FirstRoot { get; init; }

    public required IReadOnlyList<HarmonyMovement> Movements { get; init; }

    public int FirstMovementFromIndex => Movements.First().From.Index;

    int ISearchableHarmonyMovementsSequence.MovementsCount => Movements.Count;

    CompactHarmonyMovement ISearchableHarmonyMovementsSequence.GetMovement(int index) => Movements[index].Compact();

    public CompactHarmonyMovementsSequence Compact()
        => new()
        {
            Movements = Movements.Select(x => x.Compact()).ToArray(),
            FirstMovementFromIndex = Movements.First().From.Index,
            FirstRoot = FirstRoot,
        };
}