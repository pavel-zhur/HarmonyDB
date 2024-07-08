using HarmonyDB.Index.Analysis.Em.Services;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class EmTests(MusicAnalyzer analyzer, Output output)
{
    [Fact]
    public void Go()
    {
        var generatorParameters = new TestDataGeneratorParameters();
        var generator = new TestDataGenerator();
        var model = generator.GenerateTestData(generatorParameters);

        output.TraceInput(model);

        analyzer.UpdateProbabilities(model);

        output.TraceOutput(model);
    }
}