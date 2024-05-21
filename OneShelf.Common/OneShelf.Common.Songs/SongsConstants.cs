namespace OneShelf.Common.Songs;

public static class SongsConstants
{
    static SongsConstants()
    {
        HeartsByLevel = new Dictionary<int, string>
        {
            { 0, IconWhiteHeart },
            { 1, IconYellowHeart },
            { 2, IconOrangeHeart },
            { 3, IconRedHeart },
        };
    }

    public static IReadOnlyDictionary<int, string> HeartsByLevel { get; }

    public const byte MaxLevel = 3;

    public const string IconRedHeart = "❤️";
    public const string IconYellowHeart = "💛";
    public const string IconOrangeHeart = "🧡";
    public const string IconWhiteHeart = "♡";

    public static double GetRating(this IEnumerable<ILike> likes) => likes.GroupBy(x => x.UserId).Select(g => g.Max(x => x.Level)).Sum(x => x switch
    {
        1 => 1,
        2 => 1.4,
        3 => 1.8,
        _ => 0
    });
}