namespace OneShelf.Common.OpenAi.Models;

public record DialogResult(
    string Text, 
    IReadOnlyList<string> Images, 
    IReadOnlyList<VideoResult> Videos,
    IReadOnlyList<MusicResult> Music,
    IReadOnlyList<VideoLimitResult> VideoLimits,
    IReadOnlyList<MusicLimitResult> MusicLimits,
    bool IsTopicChangeDetected)
{
    public override string ToString()
    {
        return $"istc: {IsTopicChangeDetected}, text: {Text}, ima: {string.Join(", ", Images)}, vid: {Videos.Count}, mus: {Music.Count}, vidLim: {VideoLimits.Count}, musLim: {MusicLimits.Count}";
    }
}