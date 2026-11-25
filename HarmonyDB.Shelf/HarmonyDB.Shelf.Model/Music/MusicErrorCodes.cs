namespace HarmonyDB.Shelf.Model.Music;

/// <summary>
/// The error codes of the area <c>music</c>.
/// </summary>
public static class MusicErrorCodes
{
    /// <summary>404: no such music service (it is <c>yandex</c> or <c>youtube</c>).</summary>
    public const string NoSuchService = "no_such_service";

    /// <summary>400: the music service has no such page (it is Yandex's or YouTube's only).</summary>
    public const string NotOnThisService = "not_on_this_service";

    /// <summary>409: the person's own lists need their account of this service linked in the profile.</summary>
    public const string NoMusicLink = "no_music_link";

    /// <summary>409: the service refused the linked account (expired or signed out): link it anew in the profile.</summary>
    public const string MusicLinkRejected = "music_link_rejected";
}
