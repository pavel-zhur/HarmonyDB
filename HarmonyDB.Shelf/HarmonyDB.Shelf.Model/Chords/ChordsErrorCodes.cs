namespace HarmonyDB.Shelf.Model.Chords;

/// <summary>
/// The error codes of the area <c>chords</c>.
/// </summary>
public static class ChordsErrorCodes
{
    /// <summary>400: a kind of content asked is not one there is (details: <c>kind</c>).</summary>
    public const string UnknownKind = "unknown_kind";

    /// <summary>400: more ids are asked at once than allowed (details: <c>max</c>).</summary>
    public const string TooManyIds = "too_many_ids";

    /// <summary>404: the source has no picture of that name.</summary>
    public const string NoSuchIcon = "no_such_icon";
}
