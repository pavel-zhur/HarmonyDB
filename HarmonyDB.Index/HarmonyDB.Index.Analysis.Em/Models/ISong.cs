namespace HarmonyDB.Index.Analysis.Em.Models;

public interface ISong : ISource
{
    (byte Tonic, Scale Scale)? KnownTonality { get; }
}