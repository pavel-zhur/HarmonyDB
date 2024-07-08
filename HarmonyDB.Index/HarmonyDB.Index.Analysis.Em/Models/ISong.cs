namespace HarmonyDB.Index.Analysis.Em.Models;

public interface ISong : ISource
{
    bool IsTonalityKnown { get; set; }
    (int Tonic, Scale Scale) KnownTonality { get; set; }
}