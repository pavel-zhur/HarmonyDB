using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// What protects the token lookup of <c>/mcp</c> from a flood, taken before the app's rate limiter (which runs after
/// the areas' middleware, once the token is known, for the limit per connection or key):
/// <list type="bullet">
/// <item>the refused tokens of a client address are counted, and an address that has had too many in a window is
/// answered 429 before any lookup — a good token is never counted, so the people of one hosted assistant, which calls
/// from a few addresses, do not share a limit;</item>
/// <item>the lookups of everyone are limited to so many at once, with a small queue: a burst waits a moment or is
/// answered 429, and the database is not asked more than it can answer.</item>
/// </list>
/// </summary>
public sealed class McpAddressLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext> _refused;
    private readonly ConcurrencyLimiter _lookups;

    public McpAddressLimiter(IOptions<McpOptions> options)
    {
        var settings = options.Value;
        _refused = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.RefusedPerAddress, Window = settings.RateLimitWindow, QueueLimit = 0 }));
        _lookups = new(new() { PermitLimit = settings.LookupConcurrency, QueueLimit = settings.LookupQueue, QueueProcessingOrder = QueueProcessingOrder.OldestFirst });
    }

    /// <summary>
    /// Whether the address has had as many refused tokens as it may in the window.
    /// </summary>
    public bool IsBlocked(HttpContext context) => _refused.GetStatistics(context)?.CurrentAvailablePermits <= 0;

    /// <summary>
    /// Counts a refused token of the address.
    /// </summary>
    public void Refused(HttpContext context) => _refused.AttemptAcquire(context).Dispose();

    /// <summary>
    /// A place among the lookups going on, to hold while looking up; not acquired: answer 429.
    /// </summary>
    public ValueTask<RateLimitLease> LookupAsync(CancellationToken cancellationToken) => _lookups.AcquireAsync(1, cancellationToken);

    public void Dispose()
    {
        _refused.Dispose();
        _lookups.Dispose();
    }
}
