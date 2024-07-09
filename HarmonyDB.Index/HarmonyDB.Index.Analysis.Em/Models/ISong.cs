namespace HarmonyDB.Index.Analysis.Em.Models;

public interface ISong : ISource
{
    bool IsTonalityKnown { get; }
    (int Tonic, Scale Scale) KnownTonality { get; }
}