namespace HarmonyDB.Index.Analysis.Models.Index;

public record BlockJoint
{
    public required IBlock Block1 { get; init; }

    public required IBlock Block2 { get; init; }

    public int OverlapLength => Block1.EndIndex - Block2.StartIndex switch
    {
        var x and >= 0 => x,
        _ => throw new("The overlap cannot be negative."),
    };
}