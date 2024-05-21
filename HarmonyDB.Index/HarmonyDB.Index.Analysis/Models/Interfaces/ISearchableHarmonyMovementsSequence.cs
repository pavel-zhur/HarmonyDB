using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Index.Analysis.Models.Interfaces;

public interface ISearchableHarmonyMovementsSequence
{
    int FirstMovementFromIndex { get; }

    int MovementsCount { get; }

    int FirstRoot { get; }

    CompactHarmonyMovement GetMovement(int index);
}