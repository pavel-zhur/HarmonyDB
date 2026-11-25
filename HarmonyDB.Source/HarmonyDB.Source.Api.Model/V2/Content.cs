using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// The content of an item, one property per <see cref="ContentKind"/>; a kind not asked for, or not had, is null.
/// All times are seconds of the recording the material belongs to.
/// </summary>
public record Content
{
    public SheetContent? Sheet { get; init; }

    public Timeline? Timeline { get; init; }

    public Lyrics? Lyrics { get; init; }

    public Notes? Notes { get; init; }

    public Score? Score { get; init; }

    public Chart? Chart { get; init; }

    public Recording? Recording { get; init; }
}
