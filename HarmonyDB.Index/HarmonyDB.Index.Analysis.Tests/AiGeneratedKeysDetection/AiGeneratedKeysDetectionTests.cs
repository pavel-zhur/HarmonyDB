using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public class AiGeneratedKeysDetectionTests(ITestOutputHelper logger)
{
    [Fact]
    public void Go()
    {
        var program = new AiGeneratedKeysDetectionProgram(logger);
        program.Main();
    }
}