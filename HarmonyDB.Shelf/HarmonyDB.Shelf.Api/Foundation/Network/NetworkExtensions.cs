using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation.Network;

/// <summary>
/// The configuration section <c>Shelf:Proxy</c>: the reverse proxies in front of the app.
/// </summary>
public sealed class ProxyOptions
{
    public const string SectionName = "Shelf:Proxy";

    /// <summary>
    /// The networks of the reverse proxies whose <c>X-Forwarded-For</c> is believed (CIDR, <c>172.16.0.0/12</c>): the
    /// client's address is then the one the proxy saw. Must be set whenever a proxy stands in front, or every client
    /// has the proxy's address (one bucket of every rate limit). None by default: the address is the connection's. An
    /// empty entry is no entry.
    /// </summary>
    public List<string> TrustedNetworks { get; set; } = [];

    public IEnumerable<string> Networks => TrustedNetworks.Where(x => !string.IsNullOrWhiteSpace(x));
}

internal sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ProxyOptions options)
    {
        var failures = options.Networks
            .Where(x => !Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(x, out _))
            .Select(x => $"Shelf:Proxy:TrustedNetworks: '{x}' is not a network (CIDR).")
            .ToList();
        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// The client's address and the rate limits. The client's address comes from the trusted proxies only
/// (<see cref="ProxyOptions"/>); the rate limiter is one for the app: an area adds a policy of its own with
/// <c>services.AddShelfRateLimit(name, …)</c> and puts it on its endpoints with <c>.RequireRateLimiting(name)</c>.
/// A refusal is 429 <c>too_many_requests</c>.
/// </summary>
public static class NetworkExtensions
{
    internal static void AddShelfNetwork(this IServiceCollection services)
    {
        services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
    }

    /// <summary>
    /// A rate limit policy, partitioned as the area wants (by client address, by account …).
    /// </summary>
    public static IServiceCollection AddShelfRateLimit<TKey>(
        this IServiceCollection services,
        string policyName,
        Func<HttpContext, System.Threading.RateLimiting.RateLimitPartition<TKey>> partitioner)
        => services.Configure<RateLimiterOptions>(options => options.AddPolicy(policyName, partitioner));

    /// <summary>
    /// First in the pipeline: the client's address from the trusted proxies.
    /// </summary>
    internal static void UseShelfForwardedHeaders(this WebApplication app)
    {
        var networks = app.Services.GetRequiredService<IOptions<ProxyOptions>>().Value.Networks.ToList();
        if (networks.Count == 0)
            return;

        var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1 };
        forwarded.KnownNetworks.Clear();
        forwarded.KnownProxies.Clear();
        foreach (var network in networks)
            forwarded.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(network));

        app.UseForwardedHeaders(forwarded);
    }
}
