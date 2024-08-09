namespace HarmonyDB.Playground.Web.Models.Structures;

public record MatrixModel
{
    public required string ExternalId { get; init; }
    public required int ProgressionIndex { get; set; }
}