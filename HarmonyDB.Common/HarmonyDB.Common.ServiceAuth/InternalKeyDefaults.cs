namespace HarmonyDB.Common.ServiceAuth;

public static class InternalKeyDefaults
{
    /// <summary>
    /// The authentication scheme of internal clients.
    /// </summary>
    public const string Scheme = "InternalKey";

    /// <summary>
    /// The request header that carries an internal client's key.
    /// </summary>
    public const string HeaderName = "X-Internal-Key";

    /// <summary>
    /// The claim with the internal client's name (also the identity's name).
    /// </summary>
    public const string ClientClaimType = "internal_client";

    /// <summary>
    /// A claim per scope the internal client has.
    /// </summary>
    public const string ScopeClaimType = "internal_scope";
}
