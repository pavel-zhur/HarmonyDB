using HarmonyDB.Common.Representations.OneShelf;
using OneShelf.Sources.Self.Api.Services;

namespace OneShelf.Sources.Self.Api.Tests;

/// <summary>
/// The editor's text read into a sheet, and a sheet written back as the editor's text.
/// </summary>
public sealed class StructureParserTests
{
    private readonly StructureParser _parser = new();

    [Fact]
    public void TheTextIsKeptAsWritten()
    {
        var sheet = _parser.ContentToHtml("Rock & roll -> here");

        var texts = sheet.Children.Select(x => x.AsNode()).OfType<NodeText>().Select(x => x.Text).ToList();
        Assert.Contains("Rock & roll -> here", texts);
        Assert.Contains("Rock &amp; roll -&gt; here", sheet.AsHtml(new()));
    }

    [Fact]
    public void ALineOfChordsWrittenAsTextIsReadInItsColumns()
    {
        // a line of chords whose first chord is in the second column, over a line of words
        var sheet = new NodeHtml
        {
            Name = "pre",
            Children =
            {
                new NodeText(" ").AsChild(),
                new NodeChord { Children = { new NodeText("Am").AsChild() } }.AsChild(),
                new NodeText("   ").AsChild(),
                new NodeChord { Children = { new NodeText("G").AsChild() } }.AsChild(),
                new NodeText("\nla la & la").AsChild(),
            },
        };

        var text = sheet.AsOneShelf(new());
        Assert.StartsWith(">Am   G\n", text.Replace("\r\n", "\n"));

        // read back, the chords stand where they stood
        var read = _parser.ContentToHtml(text);
        Assert.Equal(text.TrimEnd(), read.AsOneShelf(new()).TrimEnd());
        Assert.StartsWith(" Am   G\nla la & la", Lines(read));
    }

    /// <summary>
    /// The sheet's text as it stands, the chords in their columns.
    /// </summary>
    private static string Lines(NodeHtml sheet)
        => string.Concat(sheet.Children.Select(x => x.AsNode() switch
        {
            NodeText text => text.Text,
            NodeChord chord => string.Concat(chord.Children.Select(c => ((NodeText)c.AsNode()).Text)),
            _ => string.Empty,
        })).Replace("\r\n", "\n");
}
