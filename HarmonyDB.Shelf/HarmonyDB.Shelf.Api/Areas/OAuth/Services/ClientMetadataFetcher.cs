using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OAuth.Services;

/// <summary>
/// Reads a client's own description from its address (a client id metadata document,
/// draft-ietf-oauth-client-id-metadata-document): https only, no redirects, no proxy, at most the draft's recommended
/// 5 kilobytes, a short time, and only to public internet addresses — an address that resolves to this machine, a
/// private network, a link-local one, or a form that embeds one (6to4, NAT64, Teredo) is refused at the connection
/// itself, so that the shelf cannot be made to call inside its own networks. A document read is kept as long as its
/// HTTP cache headers allow (at most a day); errors are never kept.
/// </summary>
public sealed class ClientMetadataFetcher(IHttpClientFactory http, IMemoryCache cache, IOptionsMonitor<OAuthOptions> options)
{
    public const string HttpClientName = "oauth-client-metadata";

    public const int MaxBytes = 5 * 1024;

    private static readonly TimeSpan MaxKept = TimeSpan.FromDays(1);

    /// <summary>
    /// The document as JSON, or null when it could not be read (any reason: the client is then not known).
    /// </summary>
    public async Task<JsonElement?> FetchAsync(Uri address, CancellationToken cancellationToken)
    {
        var key = (nameof(ClientMetadataFetcher), address.AbsoluteUri);
        if (cache.TryGetValue(key, out JsonElement kept))
            return kept;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.CurrentValue.ClientMetadataTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new MemoryStream();
            var chunk = new byte[1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                    return null;
                buffer.Write(chunk, 0, read);
            }

            using var document = JsonDocument.Parse(buffer.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var json = document.RootElement.Clone();
            if (Lifetime(response) is { } lifetime)
                cache.Set(key, json, lifetime);
            return json;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// How long the answer may be kept by its cache headers (RFC 9111: max-age, no-store, no-cache), at most a day; null:
    /// not kept.
    /// </summary>
    private static TimeSpan? Lifetime(HttpResponseMessage response)
    {
        var control = response.Headers.CacheControl;
        if (control == null || control.NoStore || control.NoCache || control.MaxAge is not { } maxAge || maxAge <= TimeSpan.Zero)
            return null;

        var age = response.Headers.Age ?? TimeSpan.Zero;
        var left = maxAge - age;
        return left <= TimeSpan.Zero ? null : left < MaxKept ? left : MaxKept;
    }

    /// <summary>
    /// The handler of its HTTP client: connects only to public addresses, follows no redirects, uses no proxy.
    /// </summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            var address = addresses.FirstOrDefault(IsPublic)
                          ?? throw new HttpRequestException($"'{context.DnsEndPoint.Host}' does not resolve to a public address.");
            if (addresses.Any(x => !IsPublic(x)))
                throw new HttpRequestException($"'{context.DnsEndPoint.Host}' resolves to a non-public address.");

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    /// <summary>
    /// Whether an address is on the public internet: not loopback, private, link-local, shared, multicast or reserved,
    /// and not one of the IPv6 forms that carry an IPv4 address inside (IPv4-mapped, 6to4, NAT64, Teredo).
    /// </summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] >= 224
                     || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                     || (b[0] == 169 && b[1] == 254)
                     || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)
                     || (b[0] == 198 && b[1] is 18 or 19)
                     || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                     || (b[0] == 203 && b[1] == 0 && b[2] == 113));
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                     || (b[0] & 0xFE) == 0xFC // unique local
                     || (b[0] == 0x20 && b[1] == 0x02) // 6to4, 2002::/16
                     || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0 && b[3] == 0) // Teredo, 2001::/32
                     || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B) // NAT64, 64:ff9b::/96 and 64:ff9b:1::/48
                     || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) // documentation, 2001:db8::/32
                     || b.Take(15).All(x => x == 0)); // ::, ::1
        }

        return false;
    }
}
