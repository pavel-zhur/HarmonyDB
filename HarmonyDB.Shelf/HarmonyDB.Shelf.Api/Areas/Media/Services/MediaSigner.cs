using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Media;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Media.Services;

/// <summary>
/// What a media address opens: a track's sound or video on a music service, or a file of the music service's store.
/// </summary>
public sealed record MediaTarget
{
    /// <summary><c>audio</c>, <c>video</c> (a track) or <c>blob</c> (a stored file).</summary>
    public required string Kind { get; init; }

    /// <summary>A track's service: <c>yandex</c> or <c>youtube</c>.</summary>
    public string? Service { get; init; }

    /// <summary>A track's id on its service, or a stored file's key.</summary>
    public required string Id { get; init; }

    public const string Audio = "audio";
    public const string Video = "video";
    public const string Blob = "blob";
}

/// <summary>
/// Short-lived signed media addresses: <c>/api/media/{payload}.{signature}</c>, the payload being what the address opens
/// and when it expires, signed with HMAC-SHA256 under a key from configuration. Whoever holds an address may play it
/// until it expires; the address is given only after the asker was shown the thing.
/// </summary>
public sealed class MediaSigner(IOptionsMonitor<MediaOptions> options, TimeProvider time)
{
    public const string PathPrefix = "/api/media/";

    /// <summary>
    /// When an address made now expires (to the second).
    /// </summary>
    public DateTimeOffset NewExpiry()
        => DateTimeOffset.FromUnixTimeSeconds((time.GetUtcNow() + options.CurrentValue.AddressLifetime).ToUnixTimeSeconds());

    /// <returns>The address.</returns>
    public string Sign(MediaTarget target, DateTimeOffset expiresAt)
    {
        var key = Key();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload(target.Kind, target.Service, target.Id, expiresAt.ToUnixTimeSeconds()));
        var signature = HMACSHA256.HashData(key, payload);
        return $"{PathPrefix}{WebEncoders.Base64UrlEncode(payload)}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    /// <summary>
    /// The address's target, when the address was signed here and has not expired.
    /// </summary>
    /// <exception cref="ShelfException">404 <c>media_invalid</c>; 410 <c>media_expired</c>.</exception>
    public (MediaTarget Target, DateTimeOffset ExpiresAt) Verify(string token)
    {
        var key = Key();
        var dot = token.IndexOf('.');
        byte[] payload, signature;
        try
        {
            if (dot <= 0) throw new FormatException();
            payload = WebEncoders.Base64UrlDecode(token[..dot]);
            signature = WebEncoders.Base64UrlDecode(token[(dot + 1)..]);
        }
        catch (FormatException)
        {
            throw ShelfException.NotFound(MediaErrorCodes.Invalid);
        }

        if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, payload), signature))
            throw ShelfException.NotFound(MediaErrorCodes.Invalid);

        var read = JsonSerializer.Deserialize<Payload>(payload)!;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(read.E);
        if (expiresAt <= time.GetUtcNow())
            throw new ShelfException(StatusCodes.Status410Gone, MediaErrorCodes.Expired);

        return (new() { Kind = read.K, Service = read.S, Id = read.I }, expiresAt);
    }

    private byte[] Key()
    {
        var key = options.CurrentValue.SigningKey;
        return string.IsNullOrEmpty(key)
            ? throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable)
            : Convert.FromBase64String(key);
    }

    private sealed record Payload(string K, string? S, string I, long E);
}

/// <summary>
/// The configuration section <c>Media</c>. With no signing key, media addresses are not given (503 <c>unavailable</c>).
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// The key media addresses are signed with: at least 32 random bytes, base64. Secret.
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>
    /// How long a media address works.
    /// </summary>
    public TimeSpan AddressLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How many requests for new media addresses one client address may make within <see cref="RateLimitWindow"/>.
    /// </summary>
    public int RateLimitPermits { get; set; } = 60;

    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);
}

internal sealed class MediaOptionsValidator : IValidateOptions<MediaOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaOptions options)
    {
        var failures = new List<string>();
        if (!string.IsNullOrEmpty(options.SigningKey))
        {
            try
            {
                if (Convert.FromBase64String(options.SigningKey).Length < 32)
                    failures.Add("Media:SigningKey is shorter than 32 bytes.");
            }
            catch (FormatException)
            {
                failures.Add("Media:SigningKey is not base64.");
            }
        }

        if (options.AddressLifetime <= TimeSpan.Zero)
            failures.Add("Media:AddressLifetime must be positive.");
        if (options.RateLimitPermits <= 0 || options.RateLimitWindow <= TimeSpan.Zero)
            failures.Add("Media: RateLimitPermits and RateLimitWindow must be positive.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
