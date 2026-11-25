using System.Security.Claims;
using System.Security.Cryptography;
using HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace HarmonyDB.Shelf.Tests.Auth;

/// <summary>
/// The id tokens of the OpenID Connect providers, signed by a key of the test standing for the provider's.
/// </summary>
public sealed class OidcProviderTests
{
    private const string GoogleIssuer = "https://accounts.google.com";
    private const string TelegramIssuer = "https://oauth.telegram.org";
    private const string ClientId = "123456789";
    private const string Nonce = "the-nonce";

    private readonly RsaSecurityKey _key = NewKey("one");
    private readonly RotatingKeys _keys;
    private readonly GoogleSignInProvider _google;
    private readonly TelegramSignInProvider _telegram;

    public OidcProviderTests()
    {
        _keys = new RotatingKeys(_key);
        var options = Options(new SignInOptions
        {
            PublicOrigin = "https://shelf.example",
            Google = { Enabled = true, ClientId = ClientId, ClientSecret = "secret" },
            Telegram = { Enabled = true, ClientId = ClientId, ClientSecret = "secret" },
        });
        _google = new(new HttpClient(), new OidcDiscovery(_keys), options, TimeProvider.System);
        _telegram = new(new HttpClient(), new OidcDiscovery(_keys), options, TimeProvider.System);
    }

    [Fact]
    public async Task GooglesGoodIdTokenGivesTheIdentity()
    {
        var identity = await _google.ValidateIdTokenAsync(Token(GoogleIssuer, GoogleClaims()), Nonce, CancellationToken.None);
        Assert.Equal("google-subject", identity.Subject);
        Assert.Equal("Ann Lee", identity.DisplayName);
        Assert.Equal("ann@example.com", identity.Email);
    }

    [Fact]
    public async Task AForgedOrForeignIdTokenIsRefused()
    {
        string[] refused =
        [
            Token(GoogleIssuer, GoogleClaims(), key: NewKey("one")),
            Token(GoogleIssuer, GoogleClaims(), audience: "another.client"),
            Token("https://accounts.example.com", GoogleClaims()),
            Token(GoogleIssuer, GoogleClaims(), expires: DateTime.UtcNow.AddMinutes(-10)),
        ];

        foreach (var token in refused)
            await Assert.ThrowsAsync<SignInProviderException>(() => _google.ValidateIdTokenAsync(token, Nonce, CancellationToken.None));

        await Assert.ThrowsAsync<SignInProviderException>(() => _google.ValidateIdTokenAsync(Token(GoogleIssuer, GoogleClaims()), "another-nonce", CancellationToken.None));

        // Google's token is not Telegram's.
        await Assert.ThrowsAsync<SignInProviderException>(() => _telegram.ValidateIdTokenAsync(Token(GoogleIssuer, TelegramClaims()), null, CancellationToken.None));
    }

    [Fact]
    public async Task TelegramsSubjectIsTheNumericUserId()
    {
        var identity = await _telegram.ValidateIdTokenAsync(Token(TelegramIssuer, TelegramClaims()), null, CancellationToken.None);
        Assert.Equal("987654321", identity.Subject);
        Assert.Equal("John Doe", identity.DisplayName);
        Assert.Equal("johndoe", identity.Username);

        // Without the profile scope's numeric id there is no identity to keep.
        var noId = TelegramClaims();
        noId.Remove("id");
        await Assert.ThrowsAsync<SignInProviderException>(() => _telegram.ValidateIdTokenAsync(Token(TelegramIssuer, noId), null, CancellationToken.None));
    }

    [Fact]
    public async Task OnlyTheSafeAlgorithmsTheDiscoveryListsAreAccepted()
    {
        // RS256 signed, but the discovery lists RS512 only; then a symmetric one only.
        _keys.Algorithms.Clear();
        _keys.Algorithms.Add(SecurityAlgorithms.RsaSha512);
        await Assert.ThrowsAsync<SignInProviderException>(() => _google.ValidateIdTokenAsync(Token(GoogleIssuer, GoogleClaims()), Nonce, CancellationToken.None));

        _keys.Algorithms.Clear();
        _keys.Algorithms.Add(SecurityAlgorithms.HmacSha256);
        await Assert.ThrowsAsync<SignInProviderException>(() => _google.ValidateIdTokenAsync(Token(GoogleIssuer, GoogleClaims()), Nonce, CancellationToken.None));
    }

    [Fact]
    public async Task AKeyNotKnownYetIsFetchedOnce()
    {
        var rotated = NewKey("two");
        _keys.Next = rotated;

        var identity = await _google.ValidateIdTokenAsync(Token(GoogleIssuer, GoogleClaims(), key: rotated), Nonce, CancellationToken.None);
        Assert.Equal("google-subject", identity.Subject);
        Assert.Equal(1, _keys.Refreshes);
    }

    private static Dictionary<string, object> GoogleClaims() => new()
    {
        ["sub"] = "google-subject",
        ["name"] = "Ann Lee",
        ["email"] = "ann@example.com",
        ["email_verified"] = true,
        ["nonce"] = Nonce,
    };

    /// <summary>
    /// As Telegram's page shows its id token.
    /// </summary>
    private static Dictionary<string, object> TelegramClaims() => new()
    {
        ["sub"] = "1234123412341234123",
        ["id"] = 987654321L,
        ["name"] = "John Doe",
        ["preferred_username"] = "johndoe",
        ["picture"] = "https://cdn4.telesco.pe/file/x.jpg",
    };

    private string Token(string issuer, Dictionary<string, object> claims, SecurityKey? key = null, string audience = ClientId, DateTime? expires = null)
    {
        var until = expires ?? DateTime.UtcNow.AddMinutes(30);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = until.AddMinutes(-60),
            NotBefore = until.AddMinutes(-60),
            Expires = until,
            Subject = new ClaimsIdentity(),
            Claims = claims,
            SigningCredentials = new SigningCredentials(key ?? _key, SecurityAlgorithms.RsaSha256),
        });
    }

    private static RsaSecurityKey NewKey(string id) => new(RSA.Create(2048)) { KeyId = id };

    internal static IOptionsMonitor<T> Options<T>(T value) => new StaticOptions<T>(value);

    private sealed class StaticOptions<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    /// <summary>
    /// The provider's published keys: the current one, and after a refresh the next.
    /// </summary>
    private sealed class RotatingKeys(SecurityKey current) : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private SecurityKey _current = current;

        public SecurityKey? Next { get; set; }

        public List<string> Algorithms { get; } = [SecurityAlgorithms.RsaSha256];

        public int Refreshes { get; private set; }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            var configuration = new OpenIdConnectConfiguration();
            configuration.SigningKeys.Add(_current);
            foreach (var algorithm in Algorithms)
                configuration.IdTokenSigningAlgValuesSupported.Add(algorithm);
            return Task.FromResult(configuration);
        }

        public void RequestRefresh()
        {
            Refreshes++;
            _current = Next ?? _current;
        }
    }
}
