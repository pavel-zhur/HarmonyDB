using System.Security.Cryptography;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Media.Services;

/// <summary>
/// What a media address opens: a track's sound or video on a music service, or a file of an item of chords, read from
/// its source through the index for the caller the address was made for.
/// </summary>
public sealed record MediaTarget
{
    /// <summary><c>audio</c>, <c>video</c> (a track) or <c>file</c> (a file of an item).</summary>
    public required string Kind { get; init; }

    /// <summary>A track's service: <c>yandex</c> or <c>youtube</c>.</summary>
    public string? Service { get; init; }

    /// <summary>A track's id on its service, or the item's id.</summary>
    public required string Id { get; init; }

    /// <summary>The file's name in its item.</summary>
    public string? Name { get; init; }

    /// <summary>For a file: whom it is read for, as they were when the address was made.</summary>
    public Caller? Caller { get; init; }

    /// <summary>For a file: the address the person came by, when they came by one.</summary>
    public Uri? Address { get; init; }

    public const string Audio = "audio";
    public const string Video = "video";
    public const string File = "file";
}

/// <summary>
/// Short-lived media addresses: <c>/api/media/{token}</c>, the token being what the address opens, for whom, and when
/// it expires, sealed (AES-GCM) under a key derived from the key in configuration: nobody can read what is in it (the
/// caller's subjects and keys, the address the person came by, among it) nor change it. Whoever holds an address may play it until it expires; the
/// address is given only after the asker was shown the thing.
/// </summary>
public sealed class MediaSigner(IOptionsMonitor<MediaOptions> options, TimeProvider time)
{
    public const string PathPrefix = "/api/media/";

    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>
    /// When an address made now expires (to the second).
    /// </summary>
    public DateTimeOffset NewExpiry()
        => DateTimeOffset.FromUnixTimeSeconds((time.GetUtcNow() + options.CurrentValue.AddressLifetime).ToUnixTimeSeconds());

    /// <returns>The address.</returns>
    public string Sign(MediaTarget target, DateTimeOffset expiresAt)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Payload(target.Kind, target.Service, target.Id, target.Name, target.Caller, target.Address, expiresAt.ToUnixTimeSeconds()), SourceV2Json.Options);
        var sealedBytes = new byte[NonceSize + plain.Length + TagSize];
        var nonce = sealedBytes.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using (var aes = new AesGcm(Key(), TagSize))
            aes.Encrypt(nonce, plain, sealedBytes.AsSpan(NonceSize, plain.Length), sealedBytes.AsSpan(NonceSize + plain.Length));

        return $"{PathPrefix}{WebEncoders.Base64UrlEncode(sealedBytes)}";
    }

    /// <summary>
    /// The address's target, when the address was made here and has not expired.
    /// </summary>
    /// <exception cref="ShelfException">404 <c>media_invalid</c>; 410 <c>media_expired</c>.</exception>
    public (MediaTarget Target, DateTimeOffset ExpiresAt) Verify(string token)
    {
        var key = Key();
        byte[] sealedBytes;
        try
        {
            sealedBytes = WebEncoders.Base64UrlDecode(token);
        }
        catch (FormatException)
        {
            throw ShelfException.NotFound(MediaErrorCodes.Invalid);
        }

        if (sealedBytes.Length <= NonceSize + TagSize)
            throw ShelfException.NotFound(MediaErrorCodes.Invalid);

        var plain = new byte[sealedBytes.Length - NonceSize - TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(sealedBytes.AsSpan(0, NonceSize), sealedBytes.AsSpan(NonceSize, plain.Length), sealedBytes.AsSpan(NonceSize + plain.Length), plain);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw ShelfException.NotFound(MediaErrorCodes.Invalid);
        }

        var read = JsonSerializer.Deserialize<Payload>(plain, SourceV2Json.Options)!;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(read.E);
        if (expiresAt <= time.GetUtcNow())
            throw new ShelfException(StatusCodes.Status410Gone, MediaErrorCodes.Expired);

        return (new() { Kind = read.K, Service = read.S, Id = read.I, Name = read.N, Caller = read.C, Address = read.A }, expiresAt);
    }

    /// <summary>
    /// The key the addresses are sealed with: derived from the configured one, for this use only.
    /// </summary>
    private byte[] Key()
        => HKDF.DeriveKey(HashAlgorithmName.SHA256, Convert.FromBase64String(options.CurrentValue.SigningKey!), 32, info: "shelf media addresses"u8.ToArray());

    private sealed record Payload(string K, string? S, string I, string? N, Caller? C, Uri? A, long E);
}
/// <summary>
/// The configuration section <c>Media</c>.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// The key media addresses are sealed with (a key for that is derived from it): at least 32 random bytes, base64. Secret.
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
        if (string.IsNullOrEmpty(options.SigningKey))
            failures.Add("Media:SigningKey is not set.");
        else
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
