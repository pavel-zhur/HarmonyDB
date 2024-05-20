using OneShelf.Frontend.Web.Models;

namespace OneShelf.Frontend.Web.Tests;

public class IndexingExtensionsTests
{
    [Fact]
    public void Test1()
    {
        Assert.Equal("A", 0.ToLetterIndex());
        Assert.Equal("Z", 25.ToLetterIndex());
        Assert.Equal("AA", 26.ToLetterIndex());
        Assert.Equal("AZ", 51.ToLetterIndex());
        Assert.Equal("BA", 52.ToLetterIndex());
        Assert.Equal("ZZ", 701.ToLetterIndex());
        Assert.Equal("702", 702.ToLetterIndex());
        Assert.Throws<ArgumentOutOfRangeException>(() => (-1).ToLetterIndex());
    }
}