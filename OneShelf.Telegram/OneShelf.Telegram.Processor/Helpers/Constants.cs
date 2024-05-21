using OneShelf.Common.Database.Songs.Model.Enums;

namespace OneShelf.Telegram.Processor.Helpers;

public static class Constants
{
    static Constants()
    {
        HeartsCallbacks = new[] { "h0", "h1", "h2", "h3", "h4" };
        MessageVersions = Enum.GetValues<MessageType>().ToDictionary(x => x, x => 1);
    }

    public static IReadOnlyList<string> HeartsCallbacks { get; }

    public static IReadOnlyDictionary<MessageType, int> MessageVersions { get; }

    public const string MarkdownV2 = "MarkdownV2";

    public const string ImproveCommandName = "chords";

    public const string IconGreenHeart = "💚";
    public const string IconList = "📄";
    public const string IconStar = "⭐";
    public const string IconCheckMark = "✅";
    public const string IconLink = "🔗";
    public const string IconAnnounce = "📣";
    public const string IconImprove = "🪄";
    public const string IconIdea = "💡";
    public const string IconLook = "🔎";
    public const string IconCross = "✖️";
    public const string IconTimes = "×";
    
    public const string FirstPart = "0";

    public const int ShortlistsPerPart = 50;
    public const int SongsPerTop = 50;
}