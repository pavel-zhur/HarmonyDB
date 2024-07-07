using HarmonyDB.Index.Analysis.Em;
using HarmonyDB.Index.Analysis.Em.Services;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class EmTests(MusicAnalyzer analyzer, Output output)
{
    [Fact]
    public void Go()
    {
        var generatorParameters = new TestDataGeneratorParameters();
        var generator = new TestDataGenerator();
        var (songs, loops, loopLinks) = generator.GenerateTestData(generatorParameters);

        analyzer.Initialize(songs.ToDictionary(kv => kv.Key, kv => (ISong)kv.Value), loops.ToDictionary(kv => kv.Key, kv => (ILoop)kv.Value), loopLinks);

        output.TraceInput(songs, loops, loopLinks);

        analyzer.UpdateProbabilities();

        output.TraceOutput(songs, loops);
    }
}