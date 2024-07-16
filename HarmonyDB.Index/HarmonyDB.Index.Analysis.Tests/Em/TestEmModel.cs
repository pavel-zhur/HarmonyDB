using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public record TestEmModel(
    IReadOnlyCollection<TestSong> Songs,
    IReadOnlyCollection<TestLoop> Loops)
    : IEmModel
{
    IReadOnlyCollection<Song> IEmModel.Songs => Songs;
    IReadOnlyCollection<Loop> IEmModel.Loops => Loops;
}