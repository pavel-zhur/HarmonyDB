using HarmonyDB.Index.Analysis.Em;
using Xunit.Abstractions;

namespace HarmonyDB.Index.Analysis.Tests;

public class EmTests(ITestOutputHelper logger)
{
    [Fact]
    public void Go()
    {
        Program.Main([]);
    }
}