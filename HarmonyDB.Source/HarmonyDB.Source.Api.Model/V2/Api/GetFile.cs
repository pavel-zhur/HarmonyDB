using System.Text.Json;
using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Source.Api.Model.V2.Api;

/// <summary>
/// A file of an item (<see cref="FileRef"/>), for the caller. Asked by <c>GET</c> (the file) or <c>HEAD</c> (its headers
/// alone), with this request in the header <see cref="HeaderName"/> and <c>Range</c> and <c>If-Range</c> as HTTP gives
/// them. Answered <c>200</c>, <c>206</c> or <c>416</c> with <c>Content-Type</c> (the reference's),
/// <c>Content-Length</c>, <c>Content-Range</c> (206, 416), <c>Accept-Ranges: bytes</c> and a strong <c>ETag</c> that
/// changes only with the bytes; <c>404</c> when there is no such item or file, or it is not open to the caller — one
/// answer, nothing more said. A file is open exactly when its item is open to the caller and the item's content, as
/// given to the caller, names it.
/// </summary>
public record GetFileRequest
{
    /// <summary>
    /// The header that carries the request: its JSON (<see cref="SourceV2Json.Options"/>) in base64url.
    /// </summary>
    public const string HeaderName = "X-Source-Request";

    public required Caller Caller { get; init; }

    public required string Id { get; init; }

    /// <summary>
    /// The file's <see cref="FileRef.Name"/>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The address the person came by, as <see cref="GetContentRequest.Address"/>: a file opens exactly when its item
    /// opens with it.
    /// </summary>
    public Uri? Address { get; init; }

    /// <summary>
    /// Whether the request is one the contract takes: a caller, an id, a name, and an address only absolute.
    /// </summary>
    public static bool IsValid(GetFileRequest request)
        => Caller.IsValid(request.Caller)
           && !string.IsNullOrEmpty(request.Id)
           && !string.IsNullOrEmpty(request.Name)
           && request.Address is null or { IsAbsoluteUri: true };

    /// <summary>
    /// The value of <see cref="HeaderName"/> for the request.
    /// </summary>
    public static string Encode(GetFileRequest request)
        => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(request, SourceV2Json.Options))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// The request carried by a value of <see cref="HeaderName"/>; null when the value is not one, or the request is not
    /// one the contract takes (<see cref="IsValid"/>).
    /// </summary>
    public static GetFileRequest? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        try
        {
            var request = JsonSerializer.Deserialize<GetFileRequest>(Convert.FromBase64String(base64), SourceV2Json.Options);
            return request != null && IsValid(request) ? request : null;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }
}
