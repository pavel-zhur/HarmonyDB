using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Network;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Mcp;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OAuth;

/// <summary>
/// The shelf as the OAuth 2.1 authorization server of its MCP server, by the MCP authorization specification
/// (2026-07-28): its metadata (RFC 8414), clients by their own description documents or by dynamic registration
/// (RFC 7591), authorization code with PKCE, refresh tokens with rotation, revocation (RFC 7009). The consent is the
/// app's page (<c>/connect</c>), which reads and answers the pending request through <c>/api/oauth/requests</c>.
/// These endpoints, outside <c>/api</c>, speak OAuth's own JSON (snake case), not the browser API's.
/// </summary>
public sealed class OAuthArea : IShelfArea
{
    public const string RateLimit = "oauth";

    private static readonly JsonSerializerOptions ProtocolJson = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OAuthOptions>().Bind(configuration.GetSection(OAuthOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OAuthOptions>, OAuthOptionsValidator>());
        services.AddSingleton<McpAddresses>();
        services.AddSingleton<SignedPayloads>();
        services.AddMemoryCache();
        services.AddHttpClient(ClientMetadataFetcher.HttpClientName).ConfigurePrimaryHttpMessageHandler(ClientMetadataFetcher.CreateHandler);
        services.AddSingleton<ClientMetadataFetcher>();
        services.AddScoped<ClientRegistry>();
        services.AddScoped<AuthorizationRequests>();
        services.AddScoped<TokenService>();

        services.AddShelfRateLimit(RateLimit, context =>
        {
            var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<OAuthOptions>>().CurrentValue;
            return RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.RateLimitPermits, Window = settings.RateLimitWindow, QueueLimit = 0 });
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(McpAddresses.AuthorizationServerMetadataPath, (McpAddresses addresses) => Results.Json(Metadata(addresses), ProtocolJson))
            .ExcludeFromDescription();

        endpoints.MapPost(McpAddresses.RegisterPath, async (HttpContext context, ClientRegistry clients, CancellationToken cancellationToken) =>
            {
                RegistrationRequest? request;
                try
                {
                    request = await context.Request.ReadFromJsonAsync<RegistrationRequest>(cancellationToken);
                }
                catch (Exception exception) when (exception is JsonException or InvalidOperationException)
                {
                    throw new OAuthError("invalid_client_metadata", "The body must be the client's metadata as JSON.");
                }

                var (client, secret) = await clients.RegisterAsync(request ?? new(), cancellationToken);
                return Results.Json(new Dictionary<string, object?>
                {
                    ["client_id"] = client.ClientId,
                    ["client_secret"] = secret,
                    ["client_id_issued_at"] = client.CreatedAt.ToUnixTimeSeconds(),
                    ["client_secret_expires_at"] = secret == null ? null : 0,
                    ["client_name"] = ClientRegistry.NameOf(client),
                    ["redirect_uris"] = client.RedirectUris,
                    ["grant_types"] = client.GrantTypes,
                    ["response_types"] = new[] { "code" },
                    ["token_endpoint_auth_method"] = client.TokenEndpointAuthMethod,
                    ["client_uri"] = client.ClientUri,
                    ["logo_uri"] = client.LogoUri,
                    ["software_id"] = client.SoftwareId,
                    ["software_version"] = client.SoftwareVersion,
                }.Where(x => x.Value != null).ToDictionary(), ProtocolJson, statusCode: StatusCodes.Status201Created);
            })
            .AddEndpointFilter(OAuthErrors)
            .RequireRateLimiting(RateLimit)
            .ExcludeFromDescription();

        endpoints.MapGet(McpAddresses.AuthorizePath, async (HttpContext context, AuthorizationRequests requests, CancellationToken cancellationToken)
                => Results.Redirect(await requests.AuthorizeAsync(context.Request.Query, cancellationToken)))
            .RequireRateLimiting(RateLimit)
            .ExcludeFromDescription();

        endpoints.MapPost(McpAddresses.TokenPath, async (HttpContext context, TokenService tokens, CancellationToken cancellationToken) =>
            {
                if (!context.Request.HasFormContentType)
                    throw new OAuthError("invalid_request", "The body must be application/x-www-form-urlencoded.");

                var response = await tokens.ExchangeAsync(context.Request, cancellationToken);
                NoStore(context);
                return Results.Json(response, ProtocolJson);
            })
            .AddEndpointFilter(OAuthErrors)
            .RequireRateLimiting(RateLimit)
            .ExcludeFromDescription();

        endpoints.MapPost(McpAddresses.RevokePath, async (HttpContext context, TokenService tokens, CancellationToken cancellationToken) =>
            {
                if (!context.Request.HasFormContentType)
                    throw new OAuthError("invalid_request", "The body must be application/x-www-form-urlencoded.");

                await tokens.RevokeAsync(context.Request, cancellationToken);
                return Results.Ok();
            })
            .AddEndpointFilter(OAuthErrors)
            .RequireRateLimiting(RateLimit)
            .ExcludeFromDescription();

        // the app's consent page: the pending request by its id, and the person's answer
        var group = endpoints.MapGroup("/api/oauth/requests").WithTags("OAuth").RequireSignIn();

        group.MapGet("/{id}", (ClaimsPrincipal user, string id, AuthorizationRequests requests, CancellationToken cancellationToken)
            => requests.GetAsync(user, id, cancellationToken)).WithName("GetAuthorizationRequest");

        group.MapPost("/{id}/approve", (ClaimsPrincipal user, string id, ApproveAuthorizationRequest request, AuthorizationRequests requests, CancellationToken cancellationToken)
            => requests.ApproveAsync(user, id, request.Permissions, cancellationToken)).WithName("ApproveAuthorizationRequest");

        group.MapPost("/{id}/deny", (ClaimsPrincipal user, string id, AuthorizationRequests requests, CancellationToken cancellationToken)
            => requests.DenyAsync(user, id, cancellationToken)).WithName("DenyAuthorizationRequest");
    }

    /// <summary>
    /// The authorization server's metadata (RFC 8414).
    /// </summary>
    private static Dictionary<string, object> Metadata(McpAddresses addresses) => new()
    {
        ["issuer"] = addresses.Issuer,
        ["authorization_endpoint"] = addresses.Url(McpAddresses.AuthorizePath),
        ["token_endpoint"] = addresses.Url(McpAddresses.TokenPath),
        ["registration_endpoint"] = addresses.Url(McpAddresses.RegisterPath),
        ["revocation_endpoint"] = addresses.Url(McpAddresses.RevokePath),
        ["response_types_supported"] = new[] { "code" },
        ["response_modes_supported"] = new[] { "query" },
        ["grant_types_supported"] = new[] { ClientRegistry.GrantAuthorizationCode, ClientRegistry.GrantRefreshToken },
        ["code_challenge_methods_supported"] = new[] { "S256" },
        ["token_endpoint_auth_methods_supported"] = new[] { ClientRegistry.AuthNone, ClientRegistry.AuthSecretBasic, ClientRegistry.AuthSecretPost },
        ["revocation_endpoint_auth_methods_supported"] = new[] { ClientRegistry.AuthNone, ClientRegistry.AuthSecretBasic, ClientRegistry.AuthSecretPost },
        ["scopes_supported"] = McpPermissions.All,
        ["client_id_metadata_document_supported"] = true,
        ["authorization_response_iss_parameter_supported"] = true,
    };

    private static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    /// <summary>
    /// Answers an <see cref="OAuthError"/> as OAuth does: <c>{ error, error_description }</c>, with
    /// <c>WWW-Authenticate</c> for a client that failed to prove itself.
    /// </summary>
    private static async ValueTask<object?> OAuthErrors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (OAuthError error)
        {
            NoStore(context.HttpContext);
            if (error.StatusCode == StatusCodes.Status401Unauthorized)
                context.HttpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"oauth\"";
            return Results.Json(new Dictionary<string, string> { ["error"] = error.Error, ["error_description"] = error.Description }, ProtocolJson, statusCode: error.StatusCode);
        }
    }
}
