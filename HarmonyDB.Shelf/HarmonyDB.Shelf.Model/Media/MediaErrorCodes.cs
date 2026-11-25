namespace HarmonyDB.Shelf.Model.Media;

/// <summary>
/// The error codes of the area <c>media</c>.
/// </summary>
public static class MediaErrorCodes
{
    /// <summary>410: the media address has expired: ask for a new one.</summary>
    public const string Expired = "media_expired";

    /// <summary>404: the media address is not one the shelf gave.</summary>
    public const string Invalid = "media_invalid";

    /// <summary>404: the item has no sound of its own (no recording, instruments or notes file).</summary>
    public const string NoMedia = "no_media";
}
