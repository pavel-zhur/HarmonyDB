using HarmonyDB.Index.Api.Client;
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

        // the relay of the source contract, version 2 (SourceV2Relay): it must name at least one source
        builder.Services
            .AddOptions<RelayV2Options>()
            .Bind(builder.Configuration.GetSection(RelayV2Options.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RelayV2Options>, RelayV2OptionsValidator>());

        // the relay keeps its own timeout per source
        builder.Services.AddHttpClient(nameof(SourceV2Relay), client => client.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddScoped<SourceV2Relay>();
    }
}
