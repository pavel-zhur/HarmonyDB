using OneShelf.Telegram.Processor.Services.Commands;
using OneShelf.Telegram.Processor.Services.Commands.Admin;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services;

public class SingletonAbstractions : ISingletonAbstractions
{
    public List<List<Type>> GetCommandsGrid()
        =>
        [
            [
                typeof(Start)
            ],

            [
                typeof(Help)
            ],


            [
                typeof(SongImages)
            ],


            [
                typeof(SongImagesTries)
            ],


            [
                typeof(Likes)
            ],

            [
                typeof(FriendsLikes)
            ],

            [
                typeof(Search)
            ],


            [
                typeof(ImproveWithParameters)
            ],


            [
                typeof(Temp),
                typeof(Bowowow)
            ],


            [
                typeof(AddSong)
            ],


            [
                typeof(AdditionalKeywords),
                typeof(ChangeSongVersion),
                typeof(Rename)
            ],


            [
                typeof(MergeSongs)
            ],


            [
                typeof(ChangeSongCategories),
                typeof(ChangeSongIsLive)
            ],


            [
                typeof(ChangeArtistCategories),
                typeof(UpdateArtistSynonyms),
                typeof(RenameArtist),
                typeof(SwapArtistAndSynonym)
            ],


            [
                typeof(MergeArtists)
            ],


            [
                typeof(QueueUpdateAll),
                typeof(MeasureAll),
                typeof(QueueDropLists)
            ],


            [
                typeof(ConfigureChatGpt)
            ],


            [
                typeof(AuthorizeWebForLastOwnChatter)
            ],


            [
                typeof(ListMultiArtistSongs),
                typeof(ListSongsWithAdditionalKeywordsOrComments)
            ],


            [
                typeof(ListDraft),
                typeof(ListLiveNoChords),
                typeof(ListPostponed),
                typeof(ListArchived)
            ],


            [
                typeof(UploadReadyOnes),
                typeof(UploadReadyOnesAll)
            ],


            [
                typeof(UpdateCommands)
            ],


            [
                typeof(Inspiration)
            ],


            [
                typeof(AskWeb)
            ],


            [
                typeof(NeverPromoteTopics)
            ]

        ];

    public Type GetDefaultCommand() => typeof(Search);

    public Type GetHelpCommand() => typeof(Help);
}