using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;

/// <summary>
/// Yandex ID, by OAuth 2.0: the authorization code flow with PKCE (S256), the scopes of the person's name, email and
/// picture only. Yandex ID gives no id token: the code is exchanged at its token endpoint (with the client's secret
/// and the PKCE verifier) and the identity is read from its user info endpoint with the access token got just now.
/// </summary>
public sealed class YandexSignInProvider(HttpClient http, IOptionsMonitor<SignInOptions> options) : ISignInProvider
{
    private const string AuthorizationEndpoint = "https://oauth.yandex.ru/authorize";
    private const string TokenEndpoint = "https://oauth.yandex.ru/token";
    private const string UserInfoEndpoint = "https://login.yandex.ru/info?format=json";

    public string Name => SignInProviders.Yandex;

    public bool IsEnabled => options.CurrentValue.Yandex.Enabled;

    public bool SendsAway => true;

    public Task<string> GetAuthorizationUrlAsync(SignInFlow flow, CancellationToken cancellationToken)
        => Task.FromResult(QueryHelpers.AddQueryString(AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = options.CurrentValue.Yandex.ClientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = "login:info login:email login:avatar",
            ["state"] = flow.State,
            ["code_challenge"] = AuthTokens.PkceChallenge(flow.CodeVerifier!),
            ["code_challenge_method"] = "S256",
            ["force_confirm"] = "yes",
        }));

    public async Task<SignInIdentity> CompleteAsync(SignInFlow flow, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken)
    {
        if (answer.TryGetValue("error", out var error))
            throw SignInProviderException.Refused($"Yandex: {error}.");

        if (!answer.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            throw SignInProviderException.Invalid("Yandex: no code.");

        var settings = options.CurrentValue.Yandex;
        try
        {
            using var tokenResponse = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["client_id"] = settings.ClientId!,
                ["client_secret"] = settings.ClientSecret!,
                ["code_verifier"] = flow.CodeVerifier!,
            }), cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
                throw SignInProviderException.Invalid($"Yandex's token endpoint answered {(int)tokenResponse.StatusCode}.");

            var tokens = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (string.IsNullOrEmpty(tokens?.AccessToken))
                throw SignInProviderException.Invalid("Yandex gave no access token.");

            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", tokens.AccessToken);
            using var infoResponse = await http.SendAsync(request, cancellationToken);
            if (!infoResponse.IsSuccessStatusCode)
                throw SignInProviderException.Invalid($"Yandex's user info answered {(int)infoResponse.StatusCode}.");

            var info = await infoResponse.Content.ReadFromJsonAsync<UserInfo>(cancellationToken);
            if (string.IsNullOrEmpty(info?.Id))
                throw SignInProviderException.Invalid("Yandex's user info has no id.");

            var name = new[] { info.DisplayName, info.RealName, info.Login }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? info.Id;
            return new(info.Id, name)
            {
                Username = info.Login,
                Email = info.DefaultEmail,
                PictureUrl = info.IsAvatarEmpty || string.IsNullOrEmpty(info.DefaultAvatarId)
                    ? null
                    : $"https://avatars.yandex.net/get-yapic/{Uri.EscapeDataString(info.DefaultAvatarId)}/islands-200",
            };
        }
        catch (HttpRequestException exception)
        {
            throw SignInProviderException.Unavailable($"Yandex: {SignInProviderException.Causes(exception)}");
        }
    }

    private string RedirectUri => $"{options.CurrentValue.PublicOrigin}/api/auth/callback/{Name}";

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record UserInfo(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("login")] string? Login,
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("real_name")] string? RealName,
        [property: JsonPropertyName("default_email")] string? DefaultEmail,
        [property: JsonPropertyName("default_avatar_id")] string? DefaultAvatarId,
        [property: JsonPropertyName("is_avatar_empty")] bool IsAvatarEmpty);
}
