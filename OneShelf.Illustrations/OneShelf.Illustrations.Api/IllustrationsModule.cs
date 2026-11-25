using HarmonyDB.Index.Api.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.OpenAi;
using OneShelf.Common.OpenAi.Models;
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

    /// <summary>
    /// The scope of the internal clients that put the new shelf's aliases (<see cref="Functions.ShelfFunctions.PutAliases"/>).
    /// </summary>
    public const string AliasesScope = "illustrations.aliases";

    public override void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddIndexApiClient(builder.Configuration);
        builder.Services.AddOpenAi(builder.Configuration);
        // with an OpenAI account the illustrations are drawn: the models that do it must be named; without one, nothing is
        // asked of the settings
        builder.Services.AddOptions<OpenAiOptions>()
            .Validate(
                x => string.IsNullOrWhiteSpace(x.OpenAiApiKey) || (!string.IsNullOrWhiteSpace(x.SongImagesPromptsModel) && !string.IsNullOrWhiteSpace(x.SongImagesModel)),
                $"{nameof(OpenAiOptions)}: with {nameof(OpenAiOptions.OpenAiApiKey)} set, {nameof(OpenAiOptions.SongImagesPromptsModel)} and {nameof(OpenAiOptions.SongImagesModel)} must be set too.")
            .ValidateOnStart();
        builder.Services.AddIllustrationsDatabase(builder.Configuration);

        builder.Services.AddScoped<AllReader>();
        builder.Services.AddScoped<IllustrationsGenerator>();
        builder.Services.AddSingleton<Functions.ShelfDrawings>();
        builder.Services
            .AddOptions<IllustrationsApiOptions>()
            .Bind(builder.Configuration.GetSection(nameof(IllustrationsApiOptions)))
            .Validate(x => x.PublicEndpoint != null, $"{nameof(IllustrationsApiOptions)}:{nameof(IllustrationsApiOptions.PublicEndpoint)} is required.");
    }
}
