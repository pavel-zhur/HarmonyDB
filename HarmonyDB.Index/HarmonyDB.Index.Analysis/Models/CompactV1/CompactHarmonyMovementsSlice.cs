using HarmonyDB.Index.Analysis.Models.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.CompactV1;

/// <summary>
/// The data of a <see cref="CompactHarmonyMovementsSequence"/> as a value, its movements a slice of a larger array.
/// </summary>
public readonly struct CompactHarmonyMovementsSlice(ReadOnlyMemory<CompactHarmonyMovement> movements, int firstMovementFromIndex, byte firstRoot)
    : ISearchableHarmonyMovementsSequence
{
    public ReadOnlyMemory<CompactHarmonyMovement> Movements { get; } = movements;

    public int FirstMovementFromIndex { get; } = firstMovementFromIndex;

    public byte FirstRoot { get; } = firstRoot;

    int ISearchableHarmonyMovementsSequence.MovementsCount => Movements.Length;

    CompactHarmonyMovement ISearchableHarmonyMovementsSequence.GetMovement(int index) => Movements.Span[index];

    public CompactHarmonyMovementsSequence ToSequence() => new()
    {
        FirstMovementFromIndex = FirstMovementFromIndex,
        FirstRoot = FirstRoot,
        Movements = Movements,
    };
}
