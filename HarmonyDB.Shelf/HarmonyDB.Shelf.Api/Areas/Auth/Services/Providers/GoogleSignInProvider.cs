using HarmonyDB.Shelf.Api.Foundation.SignIn;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;

/// <summary>
/// Google, by OpenID Connect, with the scopes <c>openid email profile</c> only. The subject is Google's <c>sub</c>; the
/// email is taken only when Google says it is verified.
/// </summary>
public sealed class GoogleSignInProvider(HttpClient http, OidcDiscovery discovery, IOptionsMonitor<SignInOptions> options, TimeProvider time)
    : OidcSignInProvider(http, discovery, options, time)
{
    public const string DiscoveryAddress = "https://accounts.google.com/.well-known/openid-configuration";

    public override string Name => SignInProviders.Google;

    protected override OAuthSignInProviderOptions Settings => SignIn.Google;

    /// <summary>
    /// Google signs its id tokens with either issuer.
    /// </summary>
    protected override IReadOnlyList<string> Issuers { get; } = ["https://accounts.google.com", "accounts.google.com"];

    protected override string Scope => "openid email profile";

    protected override bool UsesNonce => true;

    protected override string DefaultTokenEndpointAuthMethod => TokenEndpointAuthMethods.ClientSecretPost;

    protected override IReadOnlyDictionary<string, string?> ExtraAuthorizationParameters { get; } = new Dictionary<string, string?>
    {
        ["prompt"] = "select_account",
    };

    protected override SignInIdentity ReadIdentity(JsonWebToken token)
    {
        var subject = token.Subject;
        if (string.IsNullOrEmpty(subject))
            throw SignInProviderException.Invalid("Google: the id token has no subject.");

        token.TryGetPayloadValue<string>("name", out var name);
        token.TryGetPayloadValue<string>("picture", out var picture);
        token.TryGetPayloadValue<string>("email", out var email);
        token.TryGetPayloadValue<bool>("email_verified", out var emailVerified);
        var verifiedEmail = emailVerified ? email : null;

        return new(subject, !string.IsNullOrWhiteSpace(name) ? name : verifiedEmail ?? subject)
        {
            Email = verifiedEmail,
            PictureUrl = string.IsNullOrWhiteSpace(picture) ? null : picture,
        };
    }
}
