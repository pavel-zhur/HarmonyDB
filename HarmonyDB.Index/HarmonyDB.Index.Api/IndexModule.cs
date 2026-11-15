using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic;
using HarmonyDB.Index.DownstreamApi.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api;

public class IndexModule() : ApiModule("Index")
{
    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .AddCollectivesApiClient(builder.Configuration)
        .AddAuthorizationApiClient(builder.Configuration)
        .AddDownstreamApiClient(builder.Configuration)
        .AddIndexApiClient(builder.Configuration)
        .AddConcurrencyLimiter(builder.Configuration)
        .AddScoped<CommonExecutions>()
        .AddIndexBusinessLogic(builder.Configuration)
        .AddSecurityContext()
        .Configure<IndexApiOptions>(o => builder.Configuration.GetSection(nameof(IndexApiOptions)).Bind(o));
}
