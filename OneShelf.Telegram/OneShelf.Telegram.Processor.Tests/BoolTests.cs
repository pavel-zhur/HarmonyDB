namespace OneShelf.Telegram.Processor.Tests;

public class BoolTests
{
    [Fact]
    public void Test()
    {
        Assert.True(bool.Parse("True"));
        Assert.True(bool.Parse("true"));
        Assert.False(bool.Parse("False"));
        Assert.False(bool.Parse("false"));
    }
}