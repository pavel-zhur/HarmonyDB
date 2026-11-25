using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Common.ServiceAuth;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the internal clients of the configuration section <c>InternalClients</c> (validated at start), the
    /// authentication scheme <see cref="InternalKeyDefaults.Scheme"/>, and the scope check behind
    /// <see cref="RequireInternalScopeAttribute"/>.
    /// </summary>
    public static AuthenticationBuilder AddInternalClients(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<InternalClientsOptions>()
            .Configure(options => configuration.GetSection(InternalClientsOptions.SectionName).Bind(options.Clients))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<InternalClientsOptions>, InternalClientsOptionsValidator>());

        services.AddAuthorization();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, InternalScopeRequirementHandler>());

        return services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, InternalKeyAuthenticationHandler>(InternalKeyDefaults.Scheme, null);
    }
}
