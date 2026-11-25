using System.Net;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation.SignIn;

/// <summary>
/// The configuration section <c>SignIn</c>: which sign-in providers are on, their settings, the browser's sessions.
/// </summary>
public sealed class SignInOptions
{
    public const string SectionName = "SignIn";

    /// <summary>
    /// The address the app is opened at: scheme, host and port, no path (<c>https://shelf.example.com</c>). The providers
    /// return to it, and it decides the cookies' rules: on https the cookies are <c>__Host-</c> and Secure; plain http is
    /// allowed only for a loopback host in the Development environment (the local stack), where the cookies go without
    /// the prefix and the Secure flag. Not set (or empty): as on https. Needed by the providers that send the person
    /// away and back.
    /// </summary>
    public string? PublicOrigin
    {
        get => _publicOrigin;
        set => _publicOrigin = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string? _publicOrigin;

    public SessionLifetimeOptions Sessions { get; set; } = new();

    /// <summary>
    /// How long a sign-in at a provider may take, from its start to its end.
    /// </summary>
    public TimeSpan FlowLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Merging two accounts, adding a sign-in and removing one need the person signed in within this time (merging:
    /// with both identities).
    /// </summary>
    public TimeSpan FreshSignIn { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How many sign-ins one client address may start or finish within <see cref="RateLimitWindow"/>.
    /// </summary>
    public int RateLimitPermits { get; set; } = 30;

    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Telegram by OpenID Connect: the client id (the bot's id) and secret from BotFather.
    /// </summary>
    public OAuthSignInProviderOptions Telegram { get; set; } = new();

    public OAuthSignInProviderOptions Google { get; set; } = new();

    public OAuthSignInProviderOptions Yandex { get; set; } = new();

    /// <summary>
    /// Signs in as a named test account. Works only when the app runs in the Development environment as well.
    /// </summary>
    public SignInProviderOptions Dev { get; set; } = new();

    /// <summary>
    /// Whether <see cref="PublicOrigin"/> is plain http (the local stack): cookies then go without <c>__Host-</c> and
    /// Secure, which browsers do not keep on http.
    /// </summary>
    public bool IsPlainHttp => PublicOrigin != null && new Uri(PublicOrigin).Scheme == Uri.UriSchemeHttp;
}

public sealed class SessionLifetimeOptions
{
    /// <summary>
    /// A session not used for this long ends.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// A session ends this long after the sign-in that made it, used or not.
    /// </summary>
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromDays(180);
}

public class SignInProviderOptions
{
    public bool Enabled { get; set; }
}

/// <summary>
/// A provider of the authorization code flow (with PKCE).
/// </summary>
public sealed class OAuthSignInProviderOptions : SignInProviderOptions
{
    public string? ClientId { get; set; }

    /// <summary>
    /// Secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// How the client proves itself at the token endpoint (<see cref="TokenEndpointAuthMethods"/>); not set (or empty):
    /// the provider's default.
    /// </summary>
    public string? TokenEndpointAuthMethod
    {
        get => _tokenEndpointAuthMethod;
        set => _tokenEndpointAuthMethod = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string? _tokenEndpointAuthMethod;
}

/// <summary>
/// The ways a client proves itself at a token endpoint (OAuth 2.0 client authentication).
/// </summary>
public static class TokenEndpointAuthMethods
{
    /// <summary>The client id and secret in Basic authorization, each form-encoded (RFC 6749, 2.3.1).</summary>
    public const string ClientSecretBasic = "client_secret_basic";

    /// <summary>The client id and secret in the form.</summary>
    public const string ClientSecretPost = "client_secret_post";
}

/// <summary>
/// The provider names, as the API and the database (<c>account_logins.provider</c>) write them.
/// </summary>
public static class SignInProviders
{
    public const string Telegram = "telegram";
    public const string Google = "google";
    public const string Yandex = "yandex";
    public const string Dev = "dev";
}

internal sealed class SignInOptionsValidator(IHostEnvironment environment) : IValidateOptions<SignInOptions>
{
    public ValidateOptionsResult Validate(string? name, SignInOptions options)
    {
        var failures = new List<string>();

        foreach (var (provider, settings) in new[] { ("Telegram", options.Telegram), ("Google", options.Google), ("Yandex", options.Yandex) })
        {
            if (!settings.Enabled)
                continue;

            if (settings.TokenEndpointAuthMethod is not (null or TokenEndpointAuthMethods.ClientSecretBasic or TokenEndpointAuthMethods.ClientSecretPost))
                failures.Add($"SignIn:{provider}:TokenEndpointAuthMethod is neither client_secret_basic nor client_secret_post.");
            if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
                failures.Add($"SignIn:{provider} is enabled without its ClientId and ClientSecret.");
            if (options.PublicOrigin == null)
                failures.Add($"SignIn:{provider} is enabled without SignIn:PublicOrigin, where it returns to.");
        }

        if (options.PublicOrigin != null)
        {
            if (!Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin)
                || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
                || origin.PathAndQuery != "/" || options.PublicOrigin.EndsWith('/'))
            {
                failures.Add("SignIn:PublicOrigin is not an origin (scheme, host, port; no path, no slash at the end).");
            }
            else if (origin.Scheme == Uri.UriSchemeHttp && !(environment.IsDevelopment() && IsLoopback(origin.Host)))
            {
                failures.Add("SignIn:PublicOrigin is plain http: allowed only for a loopback host in the Development environment.");
            }
        }

        if (options.Sessions.IdleTimeout <= TimeSpan.Zero || options.Sessions.AbsoluteLifetime < options.Sessions.IdleTimeout)
            failures.Add("SignIn:Sessions: IdleTimeout must be positive and AbsoluteLifetime not shorter.");

        if (options.RateLimitPermits <= 0 || options.RateLimitWindow <= TimeSpan.Zero)
            failures.Add("SignIn: RateLimitPermits and RateLimitWindow must be positive.");

        if (options.FlowLifetime <= TimeSpan.Zero || options.FreshSignIn <= TimeSpan.Zero)
            failures.Add("SignIn: FlowLifetime and FreshSignIn must be positive.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsLoopback(string host)
        => host == "localhost" || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}
