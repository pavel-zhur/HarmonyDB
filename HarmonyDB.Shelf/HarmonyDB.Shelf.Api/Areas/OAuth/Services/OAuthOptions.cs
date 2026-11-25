using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OAuth.Services;

/// <summary>
/// The configuration section <c>OAuth</c>: the shelf as the authorization server of its MCP server.
/// </summary>
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    /// <summary>
    /// How long an access token works.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long a refresh token works unused; each use gives a new one.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(60);

    /// <summary>
    /// How long an authorization code may wait to be exchanged.
    /// </summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long the person may take on the consent screen.
    /// </summary>
    public TimeSpan RequestLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long reading a client's own description (a client id metadata document) may take.
    /// </summary>
    public TimeSpan ClientMetadataTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How many requests to the authorization server (registration, authorization, tokens, revocation) one client
    /// address may make within <see cref="RateLimitWindow"/>.
    /// </summary>
    public int RateLimitPermits { get; set; } = 60;

    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);
}

internal sealed class OAuthOptionsValidator : IValidateOptions<OAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, OAuthOptions options)
    {
        var failures = new List<string>();
        if (options.AccessTokenLifetime <= TimeSpan.Zero || options.RefreshTokenLifetime <= options.AccessTokenLifetime)
            failures.Add("OAuth: AccessTokenLifetime must be positive and RefreshTokenLifetime longer.");
        if (options.CodeLifetime <= TimeSpan.Zero || options.CodeLifetime > TimeSpan.FromMinutes(10))
            failures.Add("OAuth: CodeLifetime must be positive and at most 10 minutes.");
        if (options.RequestLifetime <= TimeSpan.Zero || options.ClientMetadataTimeout <= TimeSpan.Zero)
            failures.Add("OAuth: RequestLifetime and ClientMetadataTimeout must be positive.");
        if (options.RateLimitPermits <= 0 || options.RateLimitWindow <= TimeSpan.Zero)
            failures.Add("OAuth: RateLimitPermits and RateLimitWindow must be positive.");
        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
