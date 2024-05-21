using OneShelf.Common;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Telegram.Processor.Model;

namespace OneShelf.Telegram.Processor.Helpers;

public static class StringHelper
{
    public static string GetFileName(this Song song) => $"{song.Index:000} - {string.Join(", ", song.Artists.Select(x => x.Name).OrderBy(x => x))} - {song.Title}.pdf".FixFileName();

    public static string GetCaption(this Song songWithArtists, bool withIndex = true, bool withAdditionalKeywords = true)
    {
        var addition = withAdditionalKeywords
            ? songWithArtists.AdditionalKeywords
            : null;

        return
            $"{(withIndex ? $"{songWithArtists.Index:000}. " : null)}{string.Join(", ", songWithArtists.Artists.Select(x => x.Name).OrderBy(x => x))} - {songWithArtists.Title}{(string.IsNullOrWhiteSpace(addition) ? null : $" ({addition})")}";
    }

    private static string FixFileName(this string name) => Path.GetInvalidPathChars()
        .Aggregate(name, (fixedName, c) => fixedName.Replace(c, '_'));

    public static Markdown ToMarkdown(this string text) => Markdown.UnsafeFromMarkdownString("\\|#-!.()>+=_[]^~{}".Aggregate(text, (current, character) => current.Replace(character.ToString(), $"\\{character}")));

    public static Markdown BuildUrl(this Uri url, Markdown title) => url.ToString().BuildUrl(title);

    public static Markdown BuildUrl(this long userId, Markdown title) => Markdown.UnsafeFromMarkdownString($"[{title}](tg://user?id={userId})");

    public static Markdown BuildUrl(this string url, Markdown title) => Markdown.UnsafeFromMarkdownString($"[{title}]({url.Replace("(", "\\(").Replace(")", "\\)")})");

    public static Markdown Bold(this Markdown text) => Markdown.UnsafeFromMarkdownString($"*{text}*");
    public static Markdown Bold(this string text) => text.ToMarkdown().Bold();
    
    public static string SongsPluralWord(this int count)
    {
        return (count % 100) switch
        {
            0 => "песен",
            1 => "песня",
            <= 4 => "песни",
            <= 20 => "песен",
            _ => (count % 10) switch
            {
                0 => "песен",
                1 => "песня",
                <= 4 => "песни",
                _ => "песен"
            }
        };
    }

    public static string GetCategoryTitle(this string categoryTitle, string? part)
    {
        return categoryTitle + (part != null ? $" {part}" : null);
    }

    public static string GetUserTitle(this (long Id, string FirstName, string? LastName, string? Username) user)
    {
        return string
            .Join(" ", new[]
            {
                user.FirstName,
                user.LastName
            }.Where(x => !string.IsNullOrWhiteSpace(x)))
            .SelectSingle(x => string.IsNullOrWhiteSpace(x) ? user.Username : x)
            .SelectSingle(x => string.IsNullOrWhiteSpace(x) ? user.Id.ToString() : x);
    }

    public static string GetCaption(this Message message, string? title) => string.IsNullOrWhiteSpace(title)
        ? $"{message.Type} {(message.FileId != null ? "File" : "Text")} {message.Category} {message.Part} aid={message.ArtistId} sid={message.SongId}"
        : $"{message.Type} {title}";
}