namespace HarmonyDB.Shelf.Model.Me;

/// <summary>
/// The error codes of the area <c>me</c>.
/// </summary>
public static class MeErrorCodes
{
    /// <summary>409: the last sign-in of the account cannot be removed.</summary>
    public const string LastSignIn = "last_sign_in";

    /// <summary>404: no such session of the account.</summary>
    public const string NoSuchSession = "no_such_session";

    /// <summary>404: no such sign-in of the account.</summary>
    public const string NoSuchSignIn = "no_such_sign_in";

    /// <summary>404: no music account of this service is linked.</summary>
    public const string NoSuchMusicLink = "no_such_music_link";

    /// <summary>400: the music service did not accept what was copied (wrong, expired, or not signed in).</summary>
    public const string MusicCredentialsRejected = "music_credentials_rejected";

    /// <summary>400: what was pasted is larger than a music service's credentials can be (details: <c>maxBytes</c>):
    /// probably more was copied than the request's headers.</summary>
    public const string MusicCredentialsTooLarge = "music_credentials_too_large";

    /// <summary>400: a setting is out of its range (details: <c>setting</c>).</summary>
    public const string SettingInvalid = "setting_invalid";
}
