﻿using OneShelf.Telegram.Commands;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Telegram.Commands;

namespace OneShelf.Videos.Telegram.Services;

public class SingletonAbstractions : ISingletonAbstractions
{
    public List<List<Type>> GetCommandsGrid() => [
        [
            typeof(Start),
            typeof(Help),     
        ],
        [
            typeof(Step1DownloadLive),
            typeof(Step2UploadLive),
            typeof(Step3SaveInventory),
        ],
        [
            typeof(ViewTopics),
            typeof(GetFileSize),
            typeof(ListAlbums),
        ],
        [
            typeof(GoogleLogin),
        ],
        [
            typeof(UpdateCommands),
        ]
    ];

    public Type? GetDefaultCommand() => null;

    public Type GetHelpCommand() => typeof(Help);

    public Markdown GetStartResponse()
    {
        var result = new Markdown();
        result.AppendLine("Ничё не знаю.");
        return result;
    }

    public Markdown GetHelpResponseHeader()
    {
        var result = new Markdown();
        result.AppendLine("Ничё не умею.");
        return result;
    }

    public string? DialogContinuation => "Выберите следующую команду или посмотрите помощь - /help.";

    public string CommandNotFound => "Такой команды нет. Посмотрите помощь - /help.";
    
    public string BackgroundErrors => "Произошли ошибки в фоне.";

    public string BackgroundOperationComplete => "Фоновая операция завершилась.";

    public string OperationError => "Извините, случилась ошибка при выполнении операции.";

    public string? NoOperationPlaceholder => "Команда...";

    public string MiddleCommandResponsePostfix => "(или /start чтобы вернуться в начало)";
}