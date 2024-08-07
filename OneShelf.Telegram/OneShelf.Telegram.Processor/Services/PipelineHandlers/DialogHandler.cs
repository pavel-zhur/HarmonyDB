﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands;
using OneShelf.Telegram.Processor.Services.Commands.Base;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class DialogHandler : PipelineHandler
{
    private readonly ILogger<DialogHandler> _logger;
    private readonly DialogHandlerMemory _dialogHandlerMemory;
    private readonly IoFactory _ioFactory;
    private readonly IServiceProvider _serviceProvider;

    public DialogHandler(
        ILogger<DialogHandler> logger, 
        IOptions<TelegramOptions> telegramOptions, 
        SongsDatabase songsDatabase,
        DialogHandlerMemory dialogHandlerMemory,
        IoFactory ioFactory,
        IServiceProvider serviceProvider)
        : base(telegramOptions, songsDatabase)
    {
        _logger = logger;
        _dialogHandlerMemory = dialogHandlerMemory;
        _ioFactory = ioFactory;
        _serviceProvider = serviceProvider;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (!IsPrivate(update.Message?.Chat)) return false;

        var userId = update.Message!.Chat.Id;
        var isAdmin = TelegramOptions.IsAdmin(userId);
        var text = update.Message.Text?.Trim();

        SongsDatabase.Interactions.Add(new()
        {
            InteractionType = InteractionType.Dialog,
            UserId = userId,
            CreatedOn = DateTime.Now,
            Serialized = JsonConvert.SerializeObject(update),
            ShortInfoSerialized = text,
        });
        await SongsDatabase.SaveChangesAsyncX();

        if (string.IsNullOrWhiteSpace(update.Message!.Text)) return false;

        string? alias = null, parameters = null;
        if (text!.StartsWith("/"))
        {
            var until = text.IndexOfAny(new[] { ':', ' ' });
            if (until == -1)
            {
                alias = text.Substring(1);
            }
            else
            {
                alias = text.Substring(0, until).Substring(1);
                parameters = text.Substring(alias.Length + 1);

                alias = alias.Trim().ToLowerInvariant();
                parameters = parameters.Trim();

                if (string.IsNullOrWhiteSpace(alias))
                {
                    alias = null;
                    parameters = null;
                }

                if (string.IsNullOrWhiteSpace(parameters))
                {
                    parameters = null;
                }

                if (parameters?.StartsWith(':') == true)
                {
                    parameters = null;
                }
            }
        }

        if (alias == "start" && parameters?.Contains('-') == true)
        {
            alias = parameters.Substring(0, parameters.IndexOf('-'));
            parameters = parameters.Substring(parameters.IndexOf('-') + 1);
        }

        Type? command;
        if (alias == null) // continuation
        {
            var memory = _dialogHandlerMemory.Get(userId);
            if (memory != null)
            {
                memory.NewInput(text);
                _ioFactory.InitDialog(memory);
                command = _dialogHandlerMemory.GetCommands(isAdmin).Single(x =>
                    x.attribute.Alias == memory.Alias && (memory.Parameters == null
                        ? x.attribute.SupportsNoParameters
                        : x.attribute.SupportsParameters)).commandType;
            }
            else
            {
                _ioFactory.InitDialog(userId, _dialogHandlerMemory.Search.attribute.Alias, null);
                _ioFactory.Io.GetMemory().NewInput(text);
                command = _dialogHandlerMemory.Search.commandType;
            }
        }
        else // new command
        {
            command = _dialogHandlerMemory.GetCommands(isAdmin).SingleOrDefault(x =>
                x.attribute.Alias == alias &&
                (parameters != null ? x.attribute.SupportsParameters : x.attribute.SupportsNoParameters)).commandType;
            if (command == null)
            {
                var wrongAlias = alias;
                alias = _dialogHandlerMemory.Help.attribute.Alias;
                parameters = null;
                _ioFactory.InitDialog(userId, alias, parameters);
                _ioFactory.Io.WriteLine("Такая команда не найдена.");
                _ioFactory.Io.WriteLine();
                command = _dialogHandlerMemory.Help.commandType;
            }
            else
            {
                _ioFactory.InitDialog(userId, alias, parameters);
            }
        }

        var completed = false;
        IoFinish finish;
        try
        {
            var commandInstance = GetCommandInstance(command);
            await commandInstance.Execute();

            var anyScheduled = commandInstance.GetAnyScheduled();
            if (anyScheduled != null)
            {
                finish = _ioFactory.Io.GetFinishAndSwitchToMonologue();
                QueueApi(null, async api =>
                {
                    try
                    {
                        await anyScheduled;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Background tasks error.");
                        _ioFactory.Io.WriteLine("Произошли ошибки в фоне.");
                    }

                    _ioFactory.Io.WriteLine("Фоновая операция завершилась.");
                    var finish = _ioFactory.Io.FinishAndGetFinish();

                    if (!Markdown.IsNullOrWhiteSpace(finish.ReplyMessageBody))
                    {
                        await api.SendMessageAsync(new(userId, finish.ReplyMessageBody.ToString())
                        {
                            AllowSendingWithoutReply = true,
                            DisableWebPagePreview = true,
                            ParseMode = Constants.MarkdownV2,
                            ReplyToMessageId = update.Message.MessageId,
                            ReplyMarkup = finish.ReplyMessageMarkup,
                        });
                    }
                });
            }
            else
            {
                finish = _ioFactory.Io.FinishAndGetFinish();
            }

            _dialogHandlerMemory.Erase(userId);

            var startOrHelp = command == typeof(Help) || command == typeof(Start);
            if (!startOrHelp)
            {
                finish.ReplyMessageBody +=
                    $"{Environment.NewLine}Выберите следующую команду, введите часть названия песни для поиска, или посмотрите помощь - /help.";
            }

            completed = true;
        }
        catch (NeedDialogResponseException)
        {
            var memory = _ioFactory.Io.GetMemory();
            _dialogHandlerMemory.Set(userId, memory);
            finish = _ioFactory.Io.FinishAndGetFinish();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception on action execution.");
            if (isAdmin)
            {
                _ioFactory.Io.WriteLine(e.ToString());
            }
            else
            {
                _ioFactory.Io.WriteLine("Извините, у меня произошла ошибка при выполнении операции.");
            }

            completed = true;
            finish = _ioFactory.Io.FinishAndGetFinish();
            _dialogHandlerMemory.Erase(userId);
        }

        if (completed)
        {
            finish.ReplyMessageMarkup = new ReplyKeyboardMarkup(_dialogHandlerMemory.GetCommandsGrid(isAdmin)
                .Select(x => x
                    .Where(x => x.SupportsNoParameters)
                    .Select(x => new KeyboardButton($"/{x.Alias}: {x.ButtonDescription}"))))
            {
                InputFieldPlaceholder = "Команда или часть названия...",
                ResizeKeyboard = true,
            };
        }
        else
        {
            if (!finish.ReplyMessageBody.EndsWith(Environment.NewLine))
            {
                finish.ReplyMessageBody.AppendLine();
            }

            finish.ReplyMessageBody += "(или /start чтобы вернуться в начало)";
        }

        QueueApi(userId.ToString(), async api =>
        {
            foreach (var (markup, inlineKeyboardMarkup) in finish.AdditionalOutputs)
            {
                try
                {
                    await api.SendMessageAsync(new(userId, markup.ToString())
                    {
                        AllowSendingWithoutReply = true,
                        DisableWebPagePreview = true,
                        ParseMode = Constants.MarkdownV2,
                        ReplyMarkup = inlineKeyboardMarkup,
                    });
                }
                catch (BotRequestException e)
                {
                    _logger.LogError(e, "Couldn't send the message: {markup}", markup.ToString());
                    throw;
                }
            }

            try
            {
                await api.SendMessageAsync(new(userId, finish.ReplyMessageBody.ToString())
                {
                    AllowSendingWithoutReply = true,
                    DisableWebPagePreview = true,
                    ParseMode = Constants.MarkdownV2,
                    ReplyToMessageId = update.Message.MessageId,
                    ReplyMarkup = finish.ReplyMessageMarkup ?? new ReplyKeyboardRemove(),
                });
            }
            catch (BotRequestException e)
            {
                _logger.LogError(e, "Couldn't send the message: {markup}", finish.ReplyMessageBody.ToString());
                throw;
            }
        });

        return true;
    }

    private Command GetCommandInstance(Type command)
    {
        return (Command)_serviceProvider.GetRequiredService(command);
    }
}