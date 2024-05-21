namespace OneShelf.Common.OpenAi.Models;

public record DialogResult(string Text, IReadOnlyList<string> Images, bool IsTopicChangeDetected)
{
    public override string ToString()
    {
        return $"istc: {IsTopicChangeDetected}, text: {Text}, ima: {string.Join(", ", Images)}";
    }
}