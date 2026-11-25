using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Database;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Network;
using HarmonyDB.Shelf.Api.Foundation.OpenApi;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Foundation.Secrets;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation;

/// <summary>
/// What every area stands on: configuration, the database, JSON, error answers, authentication of internal clients,
/// the permission check, the OpenAPI document.
/// </summary>
public static class FoundationExtensions
{
    public static WebApplicationBuilder AddShelfFoundation(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddSingleton(TimeProvider.System);

        services.AddShelfDatabase(configuration);

        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

        AddValidatedOptions<PermissionsOptions, PermissionsOptionsValidator>(services, configuration, PermissionsOptions.SectionName);
        AddValidatedOptions<SignInOptions, SignInOptionsValidator>(services, configuration, SignInOptions.SectionName);
        AddValidatedOptions<SecretsOptions, SecretsOptionsValidator>(services, configuration, SecretsOptions.SectionName);
        AddValidatedOptions<ProxyOptions, ProxyOptionsValidator>(services, configuration, ProxyOptions.SectionName);

        // The rate limiter, one for the app: areas add their policies (NetworkExtensions.AddShelfRateLimit).
        services.AddShelfNetwork();

        // Internal clients by key; the sign-in of people adds its own schemes.
        services.AddInternalClients(configuration);
        services.AddSingleton<PermissionCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationResultHandler>();

        services.AddShelfOpenApi();

        return builder;
    }

    public static WebApplication UseShelfFoundation(this WebApplication app)
    {
        app.UseShelfForwardedHeaders();
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = ErrorResponses.HandleUnexpectedExceptionAsync });
        app.UseStatusCodePages(ErrorResponses.HandleStatusCodeAsync);
        app.Use(ErrorResponses.AnswerExpectedErrorsAsync);

        app.UseAuthentication();
        foreach (var area in app.Services.GetServices<IShelfArea>())
        {
            area.UseMiddleware(app);
        }

        app.UseRateLimiter();
        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// The JSON of the browser API: camel case, enums as camel-case strings.
    /// </summary>
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    private static void AddValidatedOptions<TOptions, TValidator>(IServiceCollection services, IConfiguration configuration, string section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddOptions<TOptions>().Bind(configuration.GetSection(section)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TOptions>, TValidator>());
    }
}
