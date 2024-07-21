namespace HarmonyDB.Index.Analysis.Models.Index.Enums;

[Flags]
public enum BlockDetections
{
    None = 0,
    UselessLoop = 1,
    UselessLoopsGroup = 2,
}