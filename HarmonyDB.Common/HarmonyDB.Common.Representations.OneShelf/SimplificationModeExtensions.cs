namespace HarmonyDB.Common.Representations.OneShelf;

public static class SimplificationModeExtensions
{
    /// <summary>
    /// The ready sets of the modes to choose from, in this order:
    /// the original chords, the simplest, simple with the basses, simple with 7, simple with 7 and the basses.
    /// </summary>
    public static IReadOnlyList<SimplificationMode> Presets { get; } =
    [
        SimplificationMode.None,
        SimplificationMode.All,
        SimplificationMode.Remove7 | SimplificationMode.Remove6 | SimplificationMode.RemoveSus,
        SimplificationMode.RemoveBass | SimplificationMode.Remove6 | SimplificationMode.RemoveSus,
        SimplificationMode.Remove9AndMore | SimplificationMode.Remove6 | SimplificationMode.RemoveSus,
    ];

    /// <summary>
    /// Switches one mode: adds it if it is not fully there, removes it otherwise.
    /// Showing 9 and more also shows 7; showing 7 keeps 9 and more hidden (and hiding 7 hides 9 and more, as <see cref="SimplificationMode.Remove7"/> includes it).
    /// </summary>
    public static SimplificationMode Switch(this SimplificationMode current, SimplificationMode mode)
    {
        var adding = !current.HasFlag(mode);

        var mode2 = mode;
        if (!adding && mode == SimplificationMode.Remove9AndMore)
            mode2 = SimplificationMode.Remove7;

        var newValue = adding
            ? current | mode2
            : current & ~mode2;

        if (!adding && mode == SimplificationMode.Remove7)
        {
            newValue |= SimplificationMode.Remove9AndMore;
        }

        return newValue;
    }
}
