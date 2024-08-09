namespace HarmonyDB.Index.Analysis.Models.Index.Matrix;

public class Matrix
{
    public required int Length { get; init; }
    public required Segment?[,] Segments { get; init; }
    public required Dictionary<string, List<(int i, int j)>> Occurrences { get; set; }
}