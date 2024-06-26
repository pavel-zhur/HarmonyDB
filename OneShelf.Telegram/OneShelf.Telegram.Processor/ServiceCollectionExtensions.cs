using HarmonyDB.Index.Api.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.OpenAi;
using OneShelf.Illustrations.Api.Client;
using OneShelf.Pdfs.Generation.Inspiration;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services;
using OneShelf.Telegram.Processor.Services.Commands;
using OneShelf.Telegram.Processor.Services.Commands.Admin;
using OneShelf.Telegram.Processor.Services.PipelineHandlers;

namespace OneShelf.Telegram.Processor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessor(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(options => configuration.Bind(nameof(TelegramOptions), options));

        services
            .AddSongsDatabase();

        services
            .AddScoped<FilesUploader>()
            .AddScoped<SimpleActions>()
            .AddScoped<ChannelActions>()
            .AddScoped<Regeneration>()
            .AddScoped<ExponentialBackOff>()
            .AddScoped<Pipeline>()
            .AddScoped<DailySelection>()
            .AddScoped<IoFactory>()
            .AddScoped(serviceProvider => serviceProvider.GetRequiredService<IoFactory>().Io)
            .AddScoped<MessageMarkdownCombiner>()
            .AddScoped<StringsCombiner>()

            .AddPdfsGenerationInspiration()

            .AddSingleton<RegenerationQueue>()
            .AddSingleton<FullTextSearch>()
            .AddSingleton<PipelineMemory>()
            .AddSingleton<DialogHandlerMemory>()

            .AddScoped<PinsRemover>()
            .AddScoped<DialogHandler>()
            .AddScoped<LikesHandler>()
            .AddScoped<InlineQueryHandler>()
            .AddScoped<PublicChatterHandler>()
            .AddScoped<PublicImportHandler>()
            .AddScoped<OwnChatterHandler>()
            .AddScoped<ChosenInlineResultCollector>()
            .AddScoped<UsersCollector>()

            .AddIndexApiClient(configuration)
            .AddIllustrationsApiClient(configuration)

            .AddScoped<Likes>()
            .AddScoped<FriendsLikes>()
            .AddScoped<SongImages>()
            .AddScoped<NeverPromoteTopics>()
            .AddScoped<AskWeb>()
            .AddScoped<SongImagesTries>()
            .AddScoped<Search>()
            .AddScoped<Help>()
            .AddScoped<Start>()
            .AddScoped<ImproveWithParameters>()

            .AddScoped<Temp>()
            .AddScoped<Bowowow>()
            .AddScoped<AddSong>()
            .AddScoped<MergeSongs>()
            .AddScoped<AdditionalKeywords>()
            .AddScoped<UpdateCommands>()
            .AddScoped<ChangeSongVersion>()
            .AddScoped<Rename>()
            .AddScoped<ChangeSongCategories>()
            .AddScoped<ChangeSongIsLive>()
            .AddScoped<ChangeArtistCategories>()
            .AddScoped<UpdateArtistSynonyms>()
            .AddScoped<RenameArtist>()
            .AddScoped<ConfigureChatGpt>()
            .AddScoped<AuthorizeWebForLastOwnChatter>()
            .AddScoped<SwapArtistAndSynonym>()
            .AddScoped<MergeArtists>()
            .AddScoped<QueueUpdateAll>()
            .AddScoped<MeasureAll>()
            .AddScoped<QueueDropLists>()
            .AddScoped<ListMultiArtistSongs>()
            .AddScoped<ListSongsWithAdditionalKeywordsOrComments>()
            .AddScoped<ListDraft>()
            .AddScoped<ListLiveNoChords>()
            .AddScoped<ListPostponed>()
            .AddScoped<ListArchived>()
            .AddScoped<UploadReadyOnes>()
            .AddScoped<UploadReadyOnesAll>()
            .AddScoped<Inspiration>()
            
            .AddOpenAi(configuration);

        return services;
    }
}