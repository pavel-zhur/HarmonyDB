namespace HarmonyDB.Shelf.Model.Auth;

/// <summary>
/// The error codes of the area <c>auth</c> (also given as <c>auth</c> in the query when a provider sends the browser
/// back).
/// </summary>
public static class AuthErrorCodes
{
    /// <summary>403: a state-changing request without the right <c>X-CSRF-Token</c> header. The answer carries a fresh
    /// token cookie: read it and repeat the request once.</summary>
    public const string CsrfFailed = "csrf_failed";

    /// <summary>404: no such provider, or it is off.</summary>
    public const string UnknownProvider = "unknown_provider";

    /// <summary>400: the sign-in is unknown, expired, finished, or not this browser's.</summary>
    public const string FlowInvalid = "sign_in_flow_invalid";

    /// <summary>400: the provider's answer did not pass the check (forged, stale, replayed, refused).</summary>
    public const string ProviderAnswerInvalid = "sign_in_answer_invalid";

    /// <summary>400: the person cancelled at the provider, or the provider refused.</summary>
    public const string ProviderRefused = "sign_in_refused";

    /// <summary>503: the provider does not answer.</summary>
    public const string ProviderUnavailable = "sign_in_provider_unavailable";

    /// <summary>403: merging needs both sign-ins made just now; sign in again with this account, then add the other
    /// sign-in again.</summary>
    public const string FreshSignInRequired = "fresh_sign_in_required";

    /// <summary>400: the return path is not a path of the app.</summary>
    public const string ReturnPathInvalid = "return_path_invalid";
}
