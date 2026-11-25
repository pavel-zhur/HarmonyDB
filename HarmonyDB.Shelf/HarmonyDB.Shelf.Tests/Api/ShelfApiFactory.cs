using System.Security.Claims;
using System.Text.Encodings.Web;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Tests.Api;

/// <summary>
/// The shelf's API in memory, with no database, a test sign-in (headers say who the caller is) and a test area of
/// endpoints that need permissions or fail.
/// </summary>
public sealed class ShelfApiFactory : WebApplicationFactory<Program>
{
    public const string AccountHeader = "X-Test-Account";
    public const string PermissionsHeader = "X-Test-Permissions";
    public const string ClientAllowedHeader = "X-Test-Client-Allowed";

    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["ConnectionStrings:Shelf"] = "Host=localhost;Database=shelf-tests",
        ["Shelf:Secrets:Key"] = Convert.ToBase64String(new byte[32]),
        ["Permissions:Custom:0:Key"] = "custom.one",
        ["Permissions:Custom:0:Label"] = "Custom one",
        ["Permissions:Defaults:Guest:0"] = "pdf",
        ["SignIn:Telegram:Enabled"] = "true",
        ["SignIn:Telegram:ClientId"] = "123456",
        ["SignIn:Telegram:ClientSecret"] = "test-secret",
        ["SignIn:PublicOrigin"] = "https://shelf.example",
        ["SignIn:Dev:Enabled"] = "true",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration(x => x.AddInMemoryCollection(Settings));
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestSignInHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestSignInHandler>(TestSignInHandler.Scheme, null);
            services.AddSingleton<IShelfArea>(new TestArea());
        });
    }

    private sealed class TestSignInHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AccountHeader, out var account))
                return Task.FromResult(AuthenticateResult.NoResult());

            var given = Request.Headers[PermissionsHeader].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            var allowed = Request.Headers.TryGetValue(ClientAllowedHeader, out var header)
                ? header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
                : null;
            var identity = ShelfIdentity.Create(Scheme, Guid.Parse(account.ToString()), given, allowed);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme)));
        }
    }

    private sealed class TestArea : IShelfArea
    {
        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/test");
            group.MapGet("/pdf", () => "ok").RequirePermission(BuiltInPermissions.Pdf);
            group.MapGet("/index", [RequirePermission(BuiltInPermissions.McpIndex)] () => "ok");
            group.MapPost("/write", () => "ok").RequireSignIn();
            group.MapGet("/not-found", string () => throw ShelfException.NotFound("no_such_playlist"));
            group.MapGet("/broken", string () => throw new InvalidOperationException("broken"));
            group.MapGet("/internal", () => "ok").RequireInternalScope("shelf.admin");
            endpoints.MapGet("/internal/test", () => "ok").RequireInternalScope("shelf.admin");
        }
    }
}
