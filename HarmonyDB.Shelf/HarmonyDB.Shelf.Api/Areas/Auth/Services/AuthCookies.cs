using System.Security.Cryptography;
using System.Text;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services;

/// <summary>
/// The cookies of signing in. On https (production) the session's and the sign-in's are <c>__Host-</c> cookies:
/// Secure, no domain, path "/". On the local stack's plain http origin (<see cref="SignInOptions.PublicOrigin"/>,
/// allowed only on a loopback host in Development) browsers keep neither the prefix nor Secure, so there they go
/// without both; every other rule is the same.
/// </summary>
public sealed class AuthCookies(IOptionsMonitor<SignInOptions> options)
{
    private bool PlainHttp => options.CurrentValue.IsPlainHttp;

    private string Prefix => PlainHttp ? string.Empty : "__Host-";

    /// <summary>
    /// The session's opaque token. HttpOnly, SameSite=Lax.
    /// </summary>
    public string SessionName => Prefix + "shelf-session";

    /// <summary>
    /// The state of the sign-in under way, binding it to this browser. HttpOnly, SameSite=Lax (it must come back with
    /// the provider's redirect).
    /// </summary>
    public string FlowName => Prefix + "shelf-flow";

    /// <summary>
    /// The CSRF token, readable by the app's script (see <see cref="CsrfTokens"/>). SameSite=Strict.
    /// </summary>
    public string CsrfName => Prefix + "shelf-csrf";

    public CookieOptions Session(TimeSpan maxAge) => Build(true, SameSiteMode.Lax, maxAge);

    public CookieOptions Flow(TimeSpan maxAge) => Build(true, SameSiteMode.Lax, maxAge);

    public CookieOptions Csrf() => Build(false, SameSiteMode.Strict, null);

    /// <summary>
    /// For deleting a cookie: the same attributes it was set with.
    /// </summary>
    public CookieOptions Expired(bool httpOnly) => Build(httpOnly, SameSiteMode.Lax, null);

    private CookieOptions Build(bool httpOnly, SameSiteMode sameSite, TimeSpan? maxAge) => new()
    {
        HttpOnly = httpOnly,
        Secure = !PlainHttp,
        SameSite = sameSite,
        Path = "/",
        MaxAge = maxAge,
        IsEssential = true,
    };
}

/// <summary>
/// Random tokens and their hashes.
/// </summary>
public static class AuthTokens
{
    /// <summary>
    /// 256 random bits, base64url.
    /// </summary>
    public static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// The hash a token is kept by (the tokens are random, so a plain SHA-256 is enough).
    /// </summary>
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    /// <summary>
    /// The PKCE challenge (S256) of a verifier.
    /// </summary>
    public static string PkceChallenge(string verifier) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
}
