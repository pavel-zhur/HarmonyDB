namespace HarmonyDB.Shelf.Model.Marks;

/// <summary>
/// A kind of mark a person puts on chords: its key (what is stored and sent) and its sign (what is drawn).
/// What a mark means, people agree among themselves.
/// </summary>
/// <param name="Heart">Whether it is one of the hearts (of colours).</param>
public sealed record MarkKind(string Key, string Sign, bool Heart = false);

/// <summary>
/// The built-in kinds of marks, in the order they are offered.
/// </summary>
public static class MarkKinds
{
    public static readonly MarkKind RedHeart = new("heart_red", "❤️", true);
    public static readonly MarkKind OrangeHeart = new("heart_orange", "🧡", true);
    public static readonly MarkKind YellowHeart = new("heart_yellow", "💛", true);
    public static readonly MarkKind GreenHeart = new("heart_green", "💚", true);
    public static readonly MarkKind BlueHeart = new("heart_blue", "💙", true);
    public static readonly MarkKind PurpleHeart = new("heart_purple", "💜", true);
    public static readonly MarkKind WhiteHeart = new("heart_white", "🤍", true);
    public static readonly MarkKind Fire = new("fire", "🔥");
    public static readonly MarkKind Star = new("star", "⭐");
    public static readonly MarkKind Guitar = new("guitar", "🎸");
    public static readonly MarkKind Microphone = new("microphone", "🎤");
    public static readonly MarkKind Campfire = new("campfire", "🏕️");
    public static readonly MarkKind Party = new("party", "🎉");
    public static readonly MarkKind Tear = new("tear", "😢");
    public static readonly MarkKind Moon = new("moon", "🌙");
    public static readonly MarkKind Dance = new("dance", "💃");
    public static readonly MarkKind Brain = new("brain", "🧠");
    public static readonly MarkKind Baby = new("baby", "👶");
    public static readonly MarkKind ChristmasTree = new("christmas_tree", "🎄");
    public static readonly MarkKind Car = new("car", "🚗");
    public static readonly MarkKind ThumbsUp = new("thumbs_up", "👍");
    public static readonly MarkKind Target = new("target", "🎯");

    public static IReadOnlyList<MarkKind> All { get; } =
    [
        RedHeart, OrangeHeart, YellowHeart, GreenHeart, BlueHeart, PurpleHeart, WhiteHeart,
        Fire, Star, Guitar, Microphone, Campfire, Party, Tear, Moon, Dance, Brain, Baby, ChristmasTree, Car, ThumbsUp, Target,
    ];

    /// <summary>
    /// The levels of like of the old shelf, as hearts: 0 white (chords kept with no level), 1 yellow, 2 orange, 3 red
    /// (its own colours of them).
    /// </summary>
    public static IReadOnlyDictionary<int, MarkKind> OldLikeLevels { get; } = new Dictionary<int, MarkKind>
    {
        [0] = WhiteHeart,
        [1] = YellowHeart,
        [2] = OrangeHeart,
        [3] = RedHeart,
    };

    public static bool IsKnown(string key) => All.Any(x => x.Key == key);
}
