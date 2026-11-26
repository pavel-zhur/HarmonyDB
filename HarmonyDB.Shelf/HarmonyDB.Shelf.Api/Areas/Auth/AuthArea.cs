using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using System.Threading.RateLimiting;
using HarmonyDB.Shelf.Api.Foundation.Network;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth;

/// <summary>
/// Signing in: the providers, the browser's sessions (the default authentication of the API), CSRF protection, adding
/// a sign-in to the account and merging accounts, signing out.
/// </summary>
public sealed class AuthArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);

        services.AddSingleton<AuthCookies>();
        services.AddSingleton<CsrfTokens>();
        services.AddScoped<SessionService>();
        services.AddScoped<SignInFlowService>();
        services.AddScoped<AccountMerger>();
        services.AddAudioClient(configuration);

        // The providers: one class each.
        AddOidcProvider(services, SignInProviders.Telegram, TelegramSignInProvider.DiscoveryAddress,
            (x, http, discovery) => new TelegramSignInProvider(http, discovery, x.GetRequiredService<IOptionsMonitor<SignInOptions>>(), x.GetRequiredService<TimeProvider>()));
        AddOidcProvider(services, SignInProviders.Google, GoogleSignInProvider.DiscoveryAddress,
            (x, http, discovery) => new GoogleSignInProvider(http, discovery, x.GetRequiredService<IOptionsMonitor<SignInOptions>>(), x.GetRequiredService<TimeProvider>()));
        services.AddHttpClient<YandexSignInProvider>();
        services.AddScoped<ISignInProvider>(x => x.GetRequiredService<YandexSignInProvider>());
        services.AddScoped<ISignInProvider, DevSignInProvider>();

        // Starting and finishing sign-ins: limited per client address.
        services.AddShelfRateLimit(SignInRateLimit, context =>
        {
            var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<SignInOptions>>().CurrentValue;
            return RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.RateLimitPermits, Window = settings.RateLimitWindow, QueueLimit = 0 });
        });
    }

    /// <summary>
    /// The rate limit of starting and finishing sign-ins.
    /// </summary>
    public const string SignInRateLimit = "sign-in";

    public void UseMiddleware(IApplicationBuilder app) => app.Use(CsrfTokens.MiddlewareAsync);

    private static void AddOidcProvider<TProvider>(
        IServiceCollection services,
        string name,
        string discoveryAddress,
        Func<IServiceProvider, HttpClient, OidcDiscovery, TProvider> create)
        where TProvider : OidcSignInProvider
    {
        var discoveryClient = $"{name}-discovery";
        services.AddHttpClient(name);
        services.AddHttpClient(discoveryClient);
        services.AddKeyedSingleton(name, (x, _) => OidcDiscovery.FromAddress(discoveryAddress, x.GetRequiredService<IHttpClientFactory>().CreateClient(discoveryClient)));
        services.AddScoped<ISignInProvider>(x => create(x, x.GetRequiredService<IHttpClientFactory>().CreateClient(name), x.GetRequiredKeyedService<OidcDiscovery>(name)));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/session", GetSession).WithName("GetSession");

        group.MapPost("/flows", (HttpContext context, StartSignInRequest request, SignInFlowService flows, CancellationToken cancellationToken)
                => flows.StartAsync(context, request, cancellationToken))
            .WithMetadata(new RequireCsrfAttribute())
            .AllowGuests()
            .RequireRateLimiting(SignInRateLimit)
            .WithName("StartSignIn");

        group.MapPost("/flows/{state}/complete", (HttpContext context, string state, CompleteSignInRequest request, SignInFlowService flows, CancellationToken cancellationToken)
                => flows.CompleteAsync(context, state, request.Answer, cancellationToken))
            .WithMetadata(new RequireCsrfAttribute())
            .AllowGuests()
            .RequireRateLimiting(SignInRateLimit)
            .WithName("CompleteSignIn");

        group.MapGet("/flows/{state}", (HttpContext context, string state, SignInFlowService flows, CancellationToken cancellationToken)
                => flows.GetMergeQuestionAsync(context, state, cancellationToken))
            .RequireSignIn()
            .WithName("GetSignInMergeQuestion");

        group.MapPost("/flows/{state}/merge", (HttpContext context, string state, SignInFlowService flows, CancellationToken cancellationToken)
                => flows.MergeAsync(context, state, cancellationToken))
            .RequireSignIn()
            .WithName("MergeAccounts");

        // Where the providers that sign in on their own site send the browser back; not called by the app.
        group.MapGet("/callback/{provider}", async (HttpContext context, string provider, SignInFlowService flows, CancellationToken cancellationToken)
                => TypedResults.Redirect(await flows.CallbackAsync(context, provider, context.Request.Query, cancellationToken)))
            .RequireRateLimiting(SignInRateLimit)
            .ExcludeFromDescription();

        group.MapPost("/sign-out", async (HttpContext context, SessionService sessions, CancellationToken cancellationToken) =>
            {
                await sessions.SignOutAsync(context, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithMetadata(new RequireCsrfAttribute())
            .AllowGuests()
            .WithName("SignOut");
    }

    private static Ok<CurrentSession> GetSession(ClaimsPrincipal user, PermissionCatalog permissions)
        => TypedResults.Ok(new CurrentSession
        {
            AccountId = user.GetAccountId(),
            Permissions = BuiltIn(permissions.InForce(user)),
        });

    /// <summary>
    /// The built-in permissions among these: the custom ones are not shown to the browser.
    /// </summary>
    internal static IReadOnlyList<string> BuiltIn(IReadOnlySet<string> inForce)
        => BuiltInPermissions.All.Where(inForce.Contains).ToList();
}
