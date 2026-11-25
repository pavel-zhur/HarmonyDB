using HarmonyDB.Index.Api.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.OpenAi;
using OneShelf.Illustrations.Api.Options;
using OneShelf.Illustrations.Api.Services;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api;

public class IllustrationsModule() : ApiModule("Illustrations")
{
    /// <summary>
    /// The scope of the internal clients (configuration <c>Illustrations:InternalClients</c>) that may call the functions
    /// of the <see cref="OneShelf.Common.Api.AuthorizationLevel.Function"/> level besides the callers with a function key.
    /// </summary>
    public const string InternalScope = "illustrations";

    public override void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddIndexApiClient(builder.Configuration);
        builder.Services.AddOpenAi(builder.Configuration);
        builder.Services.AddIllustrationsDatabase(builder.Configuration);

        builder.Services.AddScoped<AllReader>();
        builder.Services
            .AddOptions<IllustrationsApiOptions>()
            .Bind(builder.Configuration.GetSection(nameof(IllustrationsApiOptions)))
            .Validate(x => x.PublicEndpoint != null, $"{nameof(IllustrationsApiOptions)}:{nameof(IllustrationsApiOptions.PublicEndpoint)} is required.");
    }
}
