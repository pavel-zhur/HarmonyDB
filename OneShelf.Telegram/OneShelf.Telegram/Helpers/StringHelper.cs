using OneShelf.Telegram.Model;

namespace OneShelf.Telegram.Helpers;

public static class StringHelper
{
    public static Markdown ToMarkdown(this string text) => Markdown.UnsafeFromMarkdownString("\\|#-!.()>+=_[]^~{}".Aggregate(text, (current, character) => current.Replace(character.ToString(), $"\\{character}")));

    public static Markdown BuildUrl(this Uri url, Markdown title) => url.ToString().BuildUrl(title);

    public static Markdown BuildUrl(this long userId, Markdown title) => Markdown.UnsafeFromMarkdownString($"[{title}](tg://user?id={userId})");

    public static Markdown BuildUrl(this string url, Markdown title) => Markdown.UnsafeFromMarkdownString($"[{title}]({url.Replace("(", "\\(").Replace(")", "\\)")})");

    public static Markdown Bold(this Markdown text) => Markdown.UnsafeFromMarkdownString($"*{text}*");
    
    public static Markdown Bold(this string text) => text.ToMarkdown().Bold();
}