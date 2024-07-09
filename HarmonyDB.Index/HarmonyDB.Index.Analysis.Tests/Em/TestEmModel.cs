using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public record TestEmModel(
    IReadOnlyCollection<TestSong> Songs,
    IReadOnlyCollection<TestLoop> Loops,
    IReadOnlyList<TestLoopLink> LoopLinks)
    : IEmModel
{
    IReadOnlyCollection<ISong> IEmModel.Songs => Songs;
    IReadOnlyCollection<ILoop> IEmModel.Loops => Loops;
    IReadOnlyList<ILoopLink> IEmModel.LoopLinks => LoopLinks;
}