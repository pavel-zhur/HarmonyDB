namespace HarmonyDB.Common.Representations.OneShelf.Tests;

public class SimplificationModeTests
{
    [Theory]
    // hiding 7 also hides 9 and more
    [InlineData(SimplificationMode.None, SimplificationMode.Remove7, SimplificationMode.Remove7 | SimplificationMode.Remove9AndMore)]
    // showing 7 keeps 9 and more hidden
    [InlineData(SimplificationMode.Remove7, SimplificationMode.Remove7, SimplificationMode.Remove9AndMore)]
    // showing 9 and more also shows 7
    [InlineData(SimplificationMode.Remove7, SimplificationMode.Remove9AndMore, SimplificationMode.None)]
    [InlineData(SimplificationMode.Remove9AndMore, SimplificationMode.Remove9AndMore, SimplificationMode.None)]
    // hiding 9 and more keeps 7 shown
    [InlineData(SimplificationMode.None, SimplificationMode.Remove9AndMore, SimplificationMode.Remove9AndMore)]
    // the others are independent
    [InlineData(SimplificationMode.RemoveBass, SimplificationMode.RemoveSus, SimplificationMode.RemoveBass | SimplificationMode.RemoveSus)]
    [InlineData(SimplificationMode.All, SimplificationMode.Remove6, SimplificationMode.Remove7 | SimplificationMode.RemoveBass | SimplificationMode.RemoveSus)]
    public void Switch(SimplificationMode current, SimplificationMode mode, SimplificationMode expected)
    {
        Assert.Equal(expected, current.Switch(mode));
    }

    [Fact]
    public void Presets()
    {
        Assert.Equal(
            [
                SimplificationMode.None,
                SimplificationMode.All,
                SimplificationMode.Remove7 | SimplificationMode.Remove6 | SimplificationMode.RemoveSus,
                SimplificationMode.RemoveBass | SimplificationMode.Remove6 | SimplificationMode.RemoveSus,
                SimplificationMode.Remove9AndMore | SimplificationMode.Remove6 | SimplificationMode.RemoveSus,
            ],
            SimplificationModeExtensions.Presets);
    }
}
