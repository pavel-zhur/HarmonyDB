using System.Globalization;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;

/// <summary>
/// Telegram, by OpenID Connect (https://core.telegram.org/widgets/login): the client id (the bot's id) and secret are
/// given by BotFather; the client proves itself by Basic authorization; the scopes are <c>openid profile</c>. The
/// subject kept is the person's numeric Telegram user id, the claim <c>id</c> of the scope <c>profile</c> (not the
/// token's <c>sub</c>, which is another identifier), the same id Telegram's other ways of signing in give.
/// </summary>
public sealed class TelegramSignInProvider(HttpClient http, OidcDiscovery discovery, IOptionsMonitor<SignInOptions> options, TimeProvider time)
    : OidcSignInProvider(http, discovery, options, time)
{
    public const string DiscoveryAddress = "https://oauth.telegram.org/.well-known/openid-configuration";

    public override string Name => SignInProviders.Telegram;

    protected override OAuthSignInProviderOptions Settings => SignIn.Telegram;

    protected override IReadOnlyList<string> Issuers { get; } = ["https://oauth.telegram.org"];

    protected override string Scope => "openid profile";

    /// <summary>
    /// Telegram's authorization request takes no nonce; the state (bound to the browser) and PKCE protect the flow.
    /// </summary>
    protected override bool UsesNonce => false;

    protected override string DefaultTokenEndpointAuthMethod => TokenEndpointAuthMethods.ClientSecretBasic;

    protected override SignInIdentity ReadIdentity(JsonWebToken token)
    {
        if (!token.TryGetPayloadValue<long>("id", out var id) || id <= 0)
        {
            throw SignInProviderException.Invalid("Telegram: the id token has no numeric user id (the profile scope).");
        }

        var subject = id.ToString(CultureInfo.InvariantCulture);
        token.TryGetPayloadValue<string>("name", out var name);
        token.TryGetPayloadValue<string>("preferred_username", out var username);
        token.TryGetPayloadValue<string>("picture", out var picture);

        return new(subject, !string.IsNullOrWhiteSpace(name) ? name : username ?? subject)
        {
            Username = string.IsNullOrWhiteSpace(username) ? null : username,
            PictureUrl = string.IsNullOrWhiteSpace(picture) ? null : picture,
        };
    }
}
