using HarmonyDB.Common.Representations.OneShelf;

namespace HarmonyDB.Source.Api.Model.V1;

public class Chords : PublicHeaderBase
{
    public required NodeHtml Output { get; init; }

    public required string? BestTonality { get; set; }

    public required bool IsStable { get; init; }

    public required bool IsPublic { get; init; }

    public required string? UnstableErrorMessage { get; init; }
}