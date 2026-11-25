using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Secrets;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OAuth.Services;

/// <summary>
/// Short-lived signed values that carry their own content (a pending authorization request, a file's link):
/// <c>base64url(json).base64url(HMAC-SHA256)</c>, under a key derived for each purpose from the shelf's key
/// (<c>Shelf:Secrets</c>), with their expiry inside. Nothing is kept for them; a value signed under a key that has
/// since been replaced no longer opens.
/// </summary>
public sealed class SignedPayloads(IOptionsMonitor<SecretsOptions> secrets, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Sign<T>(string purpose, T payload, DateTimeOffset expiresAt)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new Envelope<T>(payload, expiresAt.ToUnixTimeSeconds()), Json);
        return $"{WebEncoders.Base64UrlEncode(json)}.{WebEncoders.Base64UrlEncode(HMACSHA256.HashData(Key(purpose), json))}";
    }

    /// <summary>
    /// The payload, when the value was signed here for this purpose and has not expired; null otherwise.
    /// </summary>
    public (T Payload, DateTimeOffset ExpiresAt)? Verify<T>(string purpose, string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 8192)
            return null;

        var dot = value.IndexOf('.');
        if (dot <= 0)
            return null;

        byte[] json, signature;
        try
        {
            json = WebEncoders.Base64UrlDecode(value[..dot]);
            signature = WebEncoders.Base64UrlDecode(value[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(Key(purpose), json), signature))
            return null;

        var envelope = JsonSerializer.Deserialize<Envelope<T>>(json, Json)!;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(envelope.ExpiresAt);
        return expiresAt > time.GetUtcNow() ? (envelope.Payload, expiresAt) : null;
    }

    private byte[] Key(string purpose)
    {
        var options = secrets.CurrentValue;
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, options.GetKey(options.KeyVersion), 32, info: Encoding.UTF8.GetBytes($"HarmonyDB.Shelf {purpose}"));
    }

    private sealed record Envelope<T>(T Payload, long ExpiresAt);
}
