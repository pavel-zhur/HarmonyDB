using HarmonyDB.Index.Analysis.Em.Services;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class EmTests(MusicAnalyzer analyzer, Output output)
{
    [Fact]
    public void Go()
    {
        var generatorParameters = new TestDataGeneratorParameters();
        var generator = new TestDataGenerator();
        var emModel = generator.GenerateTestData(generatorParameters);

        output.TraceInput(emModel);

        var emContext = analyzer.CreateContext(emModel);
        analyzer.UpdateProbabilities(emModel, emContext);

        output.TraceOutput(emModel, emContext);
    }
}