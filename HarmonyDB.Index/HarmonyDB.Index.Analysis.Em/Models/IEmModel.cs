namespace HarmonyDB.Index.Analysis.Em.Models;

public interface IEmModel
{
    IReadOnlyCollection<Song> Songs { get; }
    IReadOnlyCollection<Loop> Loops { get; }
}