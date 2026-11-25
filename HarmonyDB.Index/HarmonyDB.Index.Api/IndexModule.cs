using System.Reflection;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Functions.V2;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.Api.Services.V2;
using HarmonyDB.Index.BusinessLogic;
using HarmonyDB.Index.DownstreamApi.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api;

public class IndexModule() : ApiModule("Index")
{
    public override void Configure(WebApplicationBuilder builder)
    {
        builder.Services
            .AddCollectivesApiClient(builder.Configuration)
            .AddAuthorizationApiClient(builder.Configuration)
            .AddDownstreamApiClient(builder.Configuration)
            .AddIndexApiClient(builder.Configuration)
            .AddConcurrencyLimiter(builder.Configuration)
            .AddScoped<CommonExecutions>()
            .AddIndexBusinessLogic(builder.Configuration)
            .AddSecurityContext()
            .Configure<IndexApiOptions>(o => builder.Configuration.GetSection(nameof(IndexApiOptions)).Bind(o));

        AddRelayV2(builder.Services, builder.Configuration);
    }

    /// <summary>
    /// The relay of the source contract, version 2 (see <see cref="SourceV2Relay"/>), when the configuration has its
    /// section. Without the section the relay is not there: its functions are not mapped, and a call to them is 404.
    /// With the section, it must name at least one source.
    /// </summary>
    public static void AddRelayV2(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetSection(RelayV2Options.SectionName).Exists())
        {
            services.AddMvcCore().ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new WithoutRelayV2FeatureProvider()));
            return;
        }

        services
            .AddOptions<RelayV2Options>()
            .Bind(configuration.GetSection(RelayV2Options.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RelayV2Options>, RelayV2OptionsValidator>());

        // the relay keeps its own timeout per source
        services.AddHttpClient(nameof(SourceV2Relay), client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddScoped<SourceV2Relay>();
    }

    /// <summary>
    /// Leaves the relay's functions out of the module's endpoints.
    /// </summary>
    private sealed class WithoutRelayV2FeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Remove(typeof(RelayV2Functions).GetTypeInfo());
        }
    }
}
