namespace HarmonyDB.Index.Analysis.Models.Index.Matrix;

public record struct Segment(string Serialized, int Occurrences, int SameKeyOccurrences, float SiblingSuccessions);