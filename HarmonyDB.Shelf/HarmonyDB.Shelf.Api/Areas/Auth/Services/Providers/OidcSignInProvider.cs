using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;

/// <summary>
/// A provider of OpenID Connect: the authorization code flow with PKCE (S256) and, where the provider takes one, a
/// nonce. The code is exchanged at the provider's token endpoint (with the client's secret and the PKCE verifier), and
/// the id token is validated: signature by the provider's published keys (fetched anew once when a token names a key
/// not known yet: the provider rotated them), issuer, audience (this client), lifetime, and the nonce of this sign-in.
/// A provider says only what differs: its addresses, scopes, and how its claims make the identity.
/// </summary>
public abstract class OidcSignInProvider(
    HttpClient http,
    OidcDiscovery discovery,
    IOptionsMonitor<SignInOptions> options,
    TimeProvider time) : ISignInProvider
{
    public abstract string Name { get; }

    public bool IsEnabled => Settings.Enabled;

    public bool SendsAway => true;

    /// <summary>
    /// The sign-in settings as they are now.
    /// </summary>
    protected SignInOptions SignIn => options.CurrentValue;

    /// <summary>
    /// The client of this provider.
    /// </summary>
    protected abstract OAuthSignInProviderOptions Settings { get; }

    /// <summary>
    /// The issuers the provider signs its id tokens with.
    /// </summary>
    protected abstract IReadOnlyList<string> Issuers { get; }

    protected abstract string Scope { get; }

    /// <summary>
    /// Whether the provider takes a nonce in the authorization request (and so must give it back in the id token).
    /// </summary>
    protected abstract bool UsesNonce { get; }

    /// <summary>
    /// How the client proves itself at the token endpoint unless its settings say otherwise
    /// (<see cref="OAuthSignInProviderOptions.TokenEndpointAuthMethod"/>).
    /// </summary>
    protected abstract string DefaultTokenEndpointAuthMethod { get; }

    /// <summary>
    /// The signing algorithms accepted when the provider's discovery lists them: the asymmetric ones the library
    /// validates against the provider's published keys. A symmetric one (signed with the client's secret) or "none"
    /// is never accepted.
    /// </summary>
    public static readonly IReadOnlySet<string> SafeAlgorithms = new HashSet<string>(StringComparer.Ordinal)
    {
        SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.EcdsaSha512,
    };

    protected virtual IReadOnlyDictionary<string, string?> ExtraAuthorizationParameters { get; } = new Dictionary<string, string?>();

    /// <summary>
    /// The identity a validated id token proves.
    /// </summary>
    protected abstract SignInIdentity ReadIdentity(JsonWebToken token);

    public async Task<string> GetAuthorizationUrlAsync(SignInFlow flow, CancellationToken cancellationToken)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        var parameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = Settings.ClientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope,
            ["state"] = flow.State,
            ["code_challenge"] = AuthTokens.PkceChallenge(flow.CodeVerifier!),
            ["code_challenge_method"] = "S256",
        };
        if (UsesNonce)
            parameters["nonce"] = flow.Nonce;
        foreach (var (key, value) in ExtraAuthorizationParameters)
            parameters[key] = value;

        return QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, parameters);
    }

    public async Task<SignInIdentity> CompleteAsync(SignInFlow flow, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken)
    {
        if (answer.TryGetValue("error", out var error))
            throw SignInProviderException.Refused($"{Name}: {error}.");

        if (!answer.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            throw SignInProviderException.Invalid($"{Name}: no code.");

        var configuration = await GetConfigurationAsync(cancellationToken);
        var settings = Settings;
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = settings.ClientId!,
            ["code_verifier"] = flow.CodeVerifier!,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint);
        if ((settings.TokenEndpointAuthMethod ?? DefaultTokenEndpointAuthMethod) == TokenEndpointAuthMethods.ClientSecretBasic)
        {
            // RFC 6749, 2.3.1: each part form-encoded, then base64.
            var credentials = $"{Uri.EscapeDataString(settings.ClientId!)}:{Uri.EscapeDataString(settings.ClientSecret!)}";
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        }
        else
        {
            form["client_secret"] = settings.ClientSecret!;
        }

        request.Content = new FormUrlEncodedContent(form);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw SignInProviderException.Unavailable($"{Name}'s token endpoint: {exception.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw SignInProviderException.Invalid($"{Name}'s token endpoint answered {(int)response.StatusCode}.");

            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (string.IsNullOrEmpty(tokens?.IdToken))
                throw SignInProviderException.Invalid($"{Name} gave no id token.");

            return await ValidateIdTokenAsync(tokens.IdToken, flow.Nonce, cancellationToken);
        }
    }

    /// <summary>
    /// The id token's checks; the identity it proves.
    /// </summary>
    public async Task<SignInIdentity> ValidateIdTokenAsync(string idToken, string? nonce, CancellationToken cancellationToken)
    {
        var result = await ValidateAsync(idToken, await GetConfigurationAsync(cancellationToken));
        if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // A key not known yet: the provider rotated its keys. Fetched anew, once.
            discovery.RequestRefresh();
            result = await ValidateAsync(idToken, await GetConfigurationAsync(cancellationToken));
        }

        if (!result.IsValid)
            throw SignInProviderException.Invalid($"{Name}: the id token is not valid: {result.Exception?.Message}");

        var token = (JsonWebToken)result.SecurityToken;
        if (UsesNonce && (!token.TryGetPayloadValue<string>("nonce", out var tokenNonce) || tokenNonce != nonce))
            throw SignInProviderException.Invalid($"{Name}: the id token's nonce is not this sign-in's.");

        return ReadIdentity(token);
    }

    private Task<TokenValidationResult> ValidateAsync(string idToken, OpenIdConnectConfiguration configuration)
    {
        // The library takes an empty list as "any": with none listed that is safe, nothing is accepted.
        var algorithms = configuration.IdTokenSigningAlgValuesSupported.Where(SafeAlgorithms.Contains).ToList();
        if (algorithms.Count == 0)
            throw SignInProviderException.Invalid($"{Name}: its discovery lists no signing algorithm accepted here.");

        return new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = Issuers,
            ValidAudience = Settings.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidAlgorithms = algorithms,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            LifetimeValidator = (notBefore, expires, _, parameters) => IsAlive(notBefore, expires, parameters.ClockSkew),
        });
    }

    private string RedirectUri => $"{options.CurrentValue.PublicOrigin}/api/auth/callback/{Name}";

    private bool IsAlive(DateTime? notBefore, DateTime? expires, TimeSpan skew)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return expires != null && now < expires.Value + skew && (notBefore == null || now >= notBefore.Value - skew);
    }

    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await discovery.GetAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw SignInProviderException.Unavailable($"{Name}'s discovery document: {exception.Message}");
        }
    }

    private sealed record TokenResponse([property: JsonPropertyName("id_token")] string? IdToken);
}

/// <summary>
/// A provider's discovery document and signing keys, fetched and refreshed by the standard configuration manager.
/// </summary>
public sealed class OidcDiscovery(IConfigurationManager<OpenIdConnectConfiguration> manager)
{
    public Task<OpenIdConnectConfiguration> GetAsync(CancellationToken cancellationToken) => manager.GetConfigurationAsync(cancellationToken);

    /// <summary>
    /// The next <see cref="GetAsync"/> fetches the document and the keys anew (at most so often as the manager
    /// allows).
    /// </summary>
    public void RequestRefresh() => manager.RequestRefresh();

    public static OidcDiscovery FromAddress(string address, HttpClient http)
        => new(new ConfigurationManager<OpenIdConnectConfiguration>(
            address,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(http) { RequireHttps = true }));
}
