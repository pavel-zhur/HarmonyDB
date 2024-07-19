using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Index.Analysis.Models.Index;

public record LoopBlock : ILoopBlock
{
    public required ReadOnlyMemory<CompactHarmonyMovement> Loop { get; init; }

    public int LoopLength => Loop.Length;

    public required int StartIndex { get; init; }

    public required int EndIndex { get; init; }

    public int BlockLength => EndIndex - StartIndex + 1;

    /// <summary>
    /// Is a whole number of and only if edge chords are the same. Min = 1.
    /// Greater than 1 if at least one movement repeats twice.
    /// Equals 1 if all chords repeat once except the edge ones.
    /// </summary>
    public float Occurrences => (float)BlockLength / LoopLength;

    /// <summary>
    /// Is a whole number of and only if edge chords are the same. Min = 0.
    /// Greater than 0 if at least one movement repeats twice.
    /// Equals 0 if all chords repeat once except the edge ones.
    /// </summary>
    public float Successions => (float)BlockLength / LoopLength - 1;

    /// <summary>
    /// Greater than 1. Is a whole if all chords repeat exactly the same number of times, like A B C D A B C D A B C D.
    /// Might be a convenient indicator of significance (if value >= 2).
    /// </summary>
    public float EachChordCoveredTimes => (float)(BlockLength + 1) / LoopLength;

    public required string Normalized { get; init; }

    /// <summary>
    /// Between 0 (inclusive) and progression length (exclusive).
    /// Returns the index of the start of the original progression in the normalized progression.
    /// In other words, the number of steps the original progression is ahead or the normalized progression is behind.
    /// For invariants > 1, returns the minimal possible shift, i.e. between 0 (inclusive) and (progression length / invariants) (exclusive).
    /// </summary>
    public required int NormalizationShift { get; init; }

    /// <summary>
    /// Corresponds to the key (it is different for the same normalized progression if and only if it is in different keys).
    /// More precisely, it is the root of the first movement of the normalized sequence, wherever its corresponding original sequence movement is.
    /// </summary>
    public required byte NormalizationRoot { get; init; }
}