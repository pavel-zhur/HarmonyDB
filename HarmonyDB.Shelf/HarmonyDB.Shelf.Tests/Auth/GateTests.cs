using System.Net;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.App;
using HarmonyDB.Shelf.Model.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.Auth;

/// <summary>
/// What needs no database: who may call what.
/// </summary>
public sealed class GateTests
{
    [Fact]
    public async Task TheDevSignInDoesNotExistOutsideDevelopment()
    {
        await using var production = new AuthApiFactory(null, "Production");
        var browser = production.NewBrowser();

        var configuration = await browser.GetAsync<PublicConfiguration>("/api/app/configuration");
        Assert.DoesNotContain(configuration.SignInProviders, x => x.Name == "dev");

        await Browser.AssertError(
            await browser.SendAsync(HttpMethod.Post, "/api/auth/flows", new StartSignInRequest { Provider = "dev" }),
            HttpStatusCode.NotFound,
            AuthErrorCodes.UnknownProvider);

        // No dev flow can start, so neither a test account's name nor an existing account's id signs anyone in; the
        // provider itself is off there, switch or not.
        var provider = production.Services.CreateScope().ServiceProvider.GetServices<ISignInProvider>().Single(x => x.Name == "dev");
        Assert.False(provider.IsEnabled);
    }

    /// <summary>
    /// Every endpoint under /api that changes state says who may call it, so that one an area forgot cannot ship.
    /// </summary>
    [Fact]
    public async Task EveryStateChangingEndpointSaysWhoMayCallIt()
    {
        await using var api = new AuthApiFactory(null, "Production");
        api.CreateClient().Dispose();

        string[] unsafeMethods = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];
        var unguarded = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(x => x.RoutePattern.RawText?.TrimStart('/').StartsWith("api/", StringComparison.Ordinal) == true)
            .Where(x => x.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Any(m => unsafeMethods.Contains(m)) == true)
            .Where(x => !x.Metadata.Any(m => m is RequireSignInAttribute or RequirePermissionAttribute or RequireInternalScopeAttribute or AllowGuestsAttribute))
            .Select(x => $"{string.Join(',', x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} {x.RoutePattern.RawText}")
            .ToList();

        Assert.True(unguarded.Count == 0, "Not saying who may call them: " + string.Join("; ", unguarded));
    }

    [Fact]
    public async Task StartingSignInsIsRateLimited()
    {
        await using var api = new AuthApiFactory(null, "Production", new Dictionary<string, string?> { ["SignIn:RateLimitPermits"] = "2" });
        var browser = api.NewBrowser();
        await browser.SendAsync(HttpMethod.Get, "/api/app/configuration");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statuses.Add((await browser.SendAsync(HttpMethod.Post, "/api/auth/flows", new StartSignInRequest { Provider = "none" })).StatusCode);

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests], statuses);
    }
}
