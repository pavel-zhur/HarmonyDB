using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.AspNetCore.Authentication;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// The door of <c>/mcp</c>, before the endpoint: a request from a web page of another origin is refused (the transport's
/// protection against DNS rebinding); an address that sent too many refused tokens, or a burst beyond the lookups the
/// shelf takes at once, is answered 429 (<see cref="McpAddressLimiter"/>); a request without a valid token is answered 401 with the challenge that tells a
/// client where to authorize (<c>WWW-Authenticate: Bearer resource_metadata=…</c>, RFC 9728). With a valid token, the
/// request is the delegated person's — whatever the browser's cookies say.
/// </summary>
public static class McpGate
{
    public static async Task MiddlewareAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.Equals(McpAddresses.McpPath, StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        var addresses = context.RequestServices.GetRequiredService<McpAddresses>();
        if (context.Request.Headers.Origin is { Count: > 0 } origin && origin.ToString() != addresses.Origin)
        {
            await ErrorResponses.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);
            return;
        }

        // a flood of bad tokens from an address is answered here; the lookups of everyone wait their turn, a few at once
        var limiter = context.RequestServices.GetRequiredService<McpAddressLimiter>();
        if (limiter.IsBlocked(context))
        {
            await ErrorResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, ErrorCodes.TooManyRequests);
            return;
        }

        AuthenticateResult result;
        using (var lookup = await limiter.LookupAsync(context.RequestAborted))
        {
            if (!lookup.IsAcquired)
            {
                await ErrorResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, ErrorCodes.TooManyRequests);
                return;
            }

            result = await context.AuthenticateAsync(McpBearerHandler.SchemeName);
        }

        if (!result.Succeeded)
        {
            limiter.Refused(context);
            var challenge = $"Bearer resource_metadata=\"{addresses.ResourceMetadata}\", scope=\"{string.Join(' ', McpPermissions.All)}\"";
            if (result.Failure != null)
                challenge += ", error=\"invalid_token\", error_description=\"The token is not valid, expired, revoked, or not for this server.\"";
            context.Response.Headers.WWWAuthenticate = challenge;
            await ErrorResponses.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.SignInRequired);
            return;
        }

        context.User = result.Principal!;
        await next(context);
    }
}
