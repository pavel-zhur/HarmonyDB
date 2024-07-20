namespace HarmonyDB.Index.Analysis.Models.Index;

public interface IBlockJoint
{
    IBlockEnvironment Block1 { get; }
    IBlockEnvironment Block2 { get; }
    int OverlapLength { get; }
}