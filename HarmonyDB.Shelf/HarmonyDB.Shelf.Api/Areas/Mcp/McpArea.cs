using System.Security.Claims;
using System.Threading.RateLimiting;
using HarmonyDB.Shelf.Api.Areas.Mcp.Services;
using HarmonyDB.Shelf.Api.Areas.Mcp.Tools;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Network;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace HarmonyDB.Shelf.Api.Areas.Mcp;

/// <summary>
/// The shelf's MCP server at <c>/mcp</c> (streamable HTTP, stateless, the official C# SDK), acting as the person whose
/// token it is, by the permissions in force for that token; its protected resource metadata (RFC 9728); the links it
/// gives to what is too big for an answer; and for the app's MCP page, the person's connected assistants and keys for
/// programs. The tools are in <c>Tools/</c>: each declares its permission, and the SDK's authorization filters, with the
/// shelf's permission check, decide — a tool the caller may not use is not listed and cannot be called. Calls are rate
/// limited per connection or key.
/// </summary>
public sealed class McpArea : IShelfArea
{
    public const string RateLimit = "mcp";

    public const string Instructions =
        "The person's shelf (HarmonyDB Shelf): chords of songs (words with chords, as people play them) with playlists, " +
        "marks, keys and notes, and the music of Yandex Music and YouTube Music. You act as the person, with what they " +
        "allowed. Chords are named by chordsId (search_chords). " + LibraryTools.Model + " Lists come in small pages " +
        "(offset, limit); big data and files come as links valid a few minutes. Changes return an undo token. Errors " +
        "return a stable code and what to do.";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<McpOptions>().Bind(configuration.GetSection(McpOptions.SectionName))
            .Validate(
                x => x.RateLimitPermits > 0 && x.RateLimitWindow > TimeSpan.Zero && x.RefusedPerAddress > 0 && x.LookupConcurrency > 0 && x.LookupQueue >= 0,
                $"{McpOptions.SectionName}: RateLimitPermits, RateLimitWindow, RefusedPerAddress and LookupConcurrency must be positive, LookupQueue not negative.")
            .ValidateOnStart();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, McpBearerHandler>(McpBearerHandler.SchemeName, null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, AnyPermissionAuthorizationHandler>());
        services.AddScoped<McpPrincipals>();
        services.AddScoped<McpConnections>();
        services.AddSingleton<McpFiles>();
        services.AddSingleton<McpAddressLimiter>();

        // per connection or key: the gate has made the caller before the limiter runs
        services.AddShelfRateLimit(RateLimit, context =>
        {
            var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<McpOptions>>().CurrentValue;
            return RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(McpPrincipals.ClientClaimType)?.Value ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.RateLimitPermits, Window = settings.RateLimitWindow, QueueLimit = 0 });
        });

        foreach (var tool in IndexTools.All().Concat(LibraryTools.All()).Concat(LibraryChangeTools.All()).Concat(MusicTools.All()).Concat(BookTools.All()))
            services.AddSingleton(provider => tool.Create(provider));

        services.AddMcpServer(options =>
            {
                options.ServerInfo = new() { Name = "harmonydb-shelf", Title = "HarmonyDB Shelf", Version = "1.0.0" };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .AddAuthorizationFilters()
            .WithRequestFilters(filters => filters.AddCallToolFilter(McpErrors.Filter));
    }

    public void UseMiddleware(IApplicationBuilder app) => app.Use(McpGate.MiddlewareAsync);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(McpAddresses.ResourceMetadataPath, (McpAddresses addresses) => Results.Json(new Dictionary<string, object>
            {
                ["resource"] = addresses.Resource,
                ["authorization_servers"] = new[] { addresses.Issuer },
                ["scopes_supported"] = McpPermissions.All,
                ["bearer_methods_supported"] = new[] { "header" },
                ["resource_name"] = "HarmonyDB Shelf",
            }))
            .ExcludeFromDescription();

        // the gate (McpGate) has made the caller the token's delegated person; this states it for the endpoint too
        endpoints.MapMcp(McpAddresses.McpPath)
            .RequireAuthorization(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim(ShelfIdentity.DelegatedClaimType).Build())
            .RequireRateLimiting(RateLimit)
            .ExcludeFromDescription();

        endpoints.MapGet($"{McpAddresses.FilesPath}/{{token}}", (HttpContext context, string token, McpFiles files, CancellationToken cancellationToken)
                => files.ServeAsync(token, context.RequestServices, cancellationToken))
            .ExcludeFromDescription();

        // the app's MCP page
        var group = endpoints.MapGroup("/api/mcp").WithTags("Mcp").RequireSignIn();

        group.MapGet("", (ClaimsPrincipal user, McpConnections connections) => connections.Overview(user)).WithName("GetMcpOverview");

        group.MapGet("/clients", (ClaimsPrincipal user, McpConnections connections, CancellationToken cancellationToken)
            => connections.ListClientsAsync(user, cancellationToken)).WithName("GetMcpClients");

        group.MapPut("/clients/{id:guid}", (ClaimsPrincipal user, Guid id, UpdateMcpPermissionsRequest request, McpConnections connections, CancellationToken cancellationToken)
            => connections.UpdateClientAsync(user, id, request.Permissions, cancellationToken)).WithName("UpdateMcpClient");

        group.MapDelete("/clients/{id:guid}", async (ClaimsPrincipal user, Guid id, McpConnections connections, CancellationToken cancellationToken) =>
        {
            await connections.DisconnectAsync(user, id, cancellationToken);
            return TypedResults.NoContent();
        }).WithName("DisconnectMcpClient");

        group.MapGet("/keys", (ClaimsPrincipal user, McpConnections connections, CancellationToken cancellationToken)
            => connections.ListKeysAsync(user, cancellationToken)).WithName("GetPersonalKeys");

        group.MapPost("/keys", (ClaimsPrincipal user, CreatePersonalKeyRequest request, McpConnections connections, CancellationToken cancellationToken)
            => connections.CreateKeyAsync(user, request, cancellationToken)).WithName("CreatePersonalKey");

        group.MapDelete("/keys/{id:guid}", async (ClaimsPrincipal user, Guid id, McpConnections connections, CancellationToken cancellationToken) =>
        {
            await connections.RevokeKeyAsync(user, id, cancellationToken);
            return TypedResults.NoContent();
        }).WithName("RevokePersonalKey");
    }
}

/// <summary>
/// The configuration section <c>Mcp</c>: how many requests to <c>/mcp</c> one connection or key may make in a window,
/// how many refused tokens one client address may send in that window, and how many token lookups are made at once for
/// everyone (with how many waiting).
/// </summary>
public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    public int RateLimitPermits { get; set; } = 120;

    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);

    public int RefusedPerAddress { get; set; } = 300;

    public int LookupConcurrency { get; set; } = 16;

    public int LookupQueue { get; set; } = 64;
}
