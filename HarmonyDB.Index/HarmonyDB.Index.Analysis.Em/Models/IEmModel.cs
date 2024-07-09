namespace HarmonyDB.Index.Analysis.Em.Models;

public interface IEmModel
{
    IReadOnlyCollection<ISong> Songs { get; }
    IReadOnlyCollection<ILoop> Loops { get; }
}