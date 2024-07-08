using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public record TestEmModel : EmModel
{
    public TestEmModel(IReadOnlyDictionary<string, Song> songs, IReadOnlyDictionary<string, Loop> loops, IReadOnlyList<LoopLink> loopLinks) 
        : base(
            songs.Values.Cast<ISong>().ToDictionary(x => x.Id),
            loops.Values.Cast<ILoop>().ToDictionary(x => x.Id),
            loopLinks)
    {
        Songs = songs;
        Loops = loops;
    }

    public new IReadOnlyDictionary<string, Loop> Loops { get; }

    public new IReadOnlyDictionary<string, Song> Songs { get; }
}