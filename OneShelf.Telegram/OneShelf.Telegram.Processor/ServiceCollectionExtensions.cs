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
            .AddSongsDatabase()
            .AddTelegram<ScopedAbstractions, SingletonAbstractions>(configuration, o => 
                o
                    .AddCommand<Temp>()

                    .AddCommand<Likes>()
                    .AddCommand<FriendsLikes>()
                    .AddCommand<SongImages>()
                    .AddCommand<NeverPromoteTopics>()
                    .AddCommand<AskWeb>()
                    .AddCommand<SongImagesTries>()
                    .AddCommand<Search>()
                    .AddCommand<Help>()
                    .AddCommand<Start>()
                    .AddCommand<ImproveWithParameters>()
                    
                    .AddCommand<Bowowow>()
                    .AddCommand<AddSong>()
                    .AddCommand<MergeSongs>()
                    .AddCommand<AdditionalKeywords>()
                    .AddCommand<UpdateCommands>()
                    .AddCommand<ChangeSongVersion>()
                    .AddCommand<Rename>()
                    .AddCommand<ChangeSongCategories>()
                    .AddCommand<ChangeSongIsLive>()
                    .AddCommand<ChangeArtistCategories>()
                    .AddCommand<UpdateArtistSynonyms>()
                    .AddCommand<RenameArtist>()
                    .AddCommand<ConfigureChatGpt>()
                    .AddCommand<AuthorizeWebForLastOwnChatter>()
                    .AddCommand<SwapArtistAndSynonym>()
                    .AddCommand<MergeArtists>()
                    .AddCommand<QueueUpdateAll>()
                    .AddCommand<MeasureAll>()
                    .AddCommand<QueueDropLists>()
                    .AddCommand<ListMultiArtistSongs>()
                    .AddCommand<ListSongsWithAdditionalKeywordsOrComments>()
                    .AddCommand<ListDraft>()
                    .AddCommand<ListLiveNoChords>()
                    .AddCommand<ListPostponed>()
                    .AddCommand<ListArchived>()
                    .AddCommand<UploadReadyOnes>()
                    .AddCommand<UploadReadyOnesAll>()
                    .AddCommand<Inspiration>()

                    .AddPipelineHandlerInOrder<UsersCollector>()
                    .AddPipelineHandlerInOrder<LikesHandler>()
                    .AddPipelineHandlerInOrder<InlineQueryHandler>()
                    .AddPipelineHandlerInOrder<ChosenInlineResultCollector>()
                    .AddPipelineHandlerInOrder<PinsRemover>()
                    .AddPipelineHandlerInOrder<DialogHandler>()
                    .AddPipelineHandlerInOrder<OwnChatterHandler>()
                    .AddPipelineHandlerInOrder<PublicChatterHandler>()
                    .AddPipelineHandlerInOrder<PublicImportHandler>()
                );

        services
            .AddScoped<FilesUploader>()
            .AddScoped<SimpleActions>()
            .AddScoped<ChannelActions>()
            .AddScoped<Regeneration>()
            .AddScoped<ExponentialBackOff>()
            .AddScoped<Pipeline>()
            .AddScoped<DailySelection>()
            .AddScoped<MessageMarkdownCombiner>()
            .AddScoped<StringsCombiner>()

            .AddPdfsGenerationInspiration()

            .AddSingleton<RegenerationQueue>()
            .AddSingleton<FullTextSearch>()
            .AddSingleton<AvailableCommands>()

            .AddIndexApiClient(configuration)
            .AddIllustrationsApiClient(configuration)
            
            .AddOpenAi(configuration);

        return services;
    }
}