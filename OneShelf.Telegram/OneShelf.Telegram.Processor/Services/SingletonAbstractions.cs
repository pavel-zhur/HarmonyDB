﻿using Microsoft.Extensions.Options;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services.Commands;
using OneShelf.Telegram.Processor.Services.Commands.Admin;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services;

public class SingletonAbstractions : ISingletonAbstractions
{
    private readonly TelegramOptions _options;

    public SingletonAbstractions(IOptions<TelegramOptions> options)
    {
        _options = options.Value;
    }

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

    public Type? GetDefaultCommand() => typeof(Search);

    public Type GetHelpCommand() => typeof(Help);

    public Markdown GetStartResponse()
    {
        var result = new Markdown();
        result.AppendLine("Добрый день!".Bold());
        result.AppendLine();
        result.Append("Для быстрого поиска песни,".Bold());
        result.AppendLine(" введите номер или часть названия или исполнителя.");
        result.AppendLine();
        result.AppendLine("Или выберите следующую команду, или посмотрите помощь - /help.");
        return result;
    }

    public Markdown GetHelpResponseHeader()
    {
        var result = new Markdown();
        result.AppendLine("Добрый день!".Bold());
        result.AppendLine();
        result.Append("Для быстрого поиска песни, введите часть".Bold());
        result.AppendLine(" названия или исполнителя или номер песни.");
        result.AppendLine();
        result.AppendLine($"Еще вы можете упомянуть меня в любом диалоге (написать @{_options.BotUsername}), чтобы найти песню и аккорды к ней.");
        result.AppendLine();
        result.AppendLine("Помимо этого, вот чем я могу помочь:");
        return result;
    }

    public string GetDialogContinuation() =>
        "Выберите следующую команду, введите часть названия песни для поиска, или посмотрите помощь - /help.";
}