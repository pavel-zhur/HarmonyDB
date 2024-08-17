using OneShelf.Telegram.Model;
using OneShelf.Telegram.Processor.Model;

namespace OneShelf.Telegram.Processor.Tests;

public class MarkdownTests
{
    [Fact]
    public void EndsWith()
    {
        var markup = new Markdown();
        Assert.True(markup.EndsWith(new()));
        Assert.False(markup.EndsWith("1"));
        Assert.False(markup.EndsWith("1"));
        markup.Append("1");
        Assert.True(markup.EndsWith(new()));
        Assert.True(markup.EndsWith("1"));
        markup.Append("2");
        Assert.True(markup.EndsWith(new()));
        Assert.False(markup.EndsWith("1"));
        markup.Append("1");
        Assert.True(markup.EndsWith(new()));
        Assert.True(markup.EndsWith("1"));
        Assert.False(markup.EndsWith("xxxxxxx"));
    }
}