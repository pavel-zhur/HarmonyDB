﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.UpdatingMessages;

namespace OneShelf.Telegram.Processor.Services;

public class ChannelActions
{
    private readonly ILogger<ChannelActions> _logger;
    private readonly ExponentialBackOff _exponentialBackOff;
    private readonly TelegramOptions _telegramOptions;
    private readonly StringsCombiner _stringsCombiner;

    public ChannelActions(ILogger<ChannelActions> logger, IOptions<TelegramOptions> telegramOptions, ExponentialBackOff exponentialBackOff, StringsCombiner stringsCombiner)
    {
        _logger = logger;
        _exponentialBackOff = exponentialBackOff;
        _stringsCombiner = stringsCombiner;
        _telegramOptions = telegramOptions.Value;
    }

    public async Task<bool> Delete(int messageId)
    {
        var api = new BotClient(_telegramOptions.Token);

        return await _exponentialBackOff.WithRetry(async () =>
        {
            try
            {
                await api.DeleteMessageAsync(
                    _telegramOptions.PublicChatId,
                    messageId);
                return true;
            }
            catch (BotRequestException e) when (e.Message.Contains("message to delete not found"))
            {
                _logger.LogInformation($"Message to delete, {messageId}, not found. Ok.");
                return true;
            }
            catch (BotRequestException e) when (e.Message.Contains("message can't be deleted"))
            {
                await api.SendMessageAsync(
                    _telegramOptions.AdminId,
                    $"Hey man, please manually delete {_stringsCombiner.BuildUrl("this message", messageId)}\\! Thanks\\.",
                    parseMode: Constants.MarkdownV2);
                return false;
            }
        });
    }

    public async Task<int> PublishTextMessage(Markdown markdownV2, InlineKeyboardMarkup? inlineKeyboardMarkup, bool inAnnouncementsTopic = false)
    {
        var api = new BotClient(_telegramOptions.Token);

        return await _exponentialBackOff.WithRetry(async () =>
        {
            try
            {
                var messageResult = await api.SendMessageAsync(new(_telegramOptions.PublicChatId, markdownV2.ToString())
                {
                    DisableNotification = true,
                    ParseMode = Constants.MarkdownV2,
                    MessageThreadId = inAnnouncementsTopic
                        ? _telegramOptions.AnnouncementsTopicId
                        : _telegramOptions.PublicTopicId,
                    ReplyMarkup = inlineKeyboardMarkup,
                    DisableWebPagePreview = true,
                });

                return messageResult.MessageId;
            }
            catch (BotRequestException e) when (e.Message.Contains("can't parse entities"))
            {
                _logger.LogError(e, "text was: {text}", markdownV2.ToString());
                throw;
            }
        });
    }

    public async Task<int> PublishFileMessage(string fileId, Markdown markdownV2, InlineKeyboardMarkup? inlineKeyboardMarkup)
    {
        if (string.IsNullOrWhiteSpace(fileId)) throw new ArgumentNullException(nameof(fileId));
        if (Markdown.IsNullOrWhiteSpace(markdownV2)) throw new ArgumentNullException(nameof(markdownV2));

        var api = new BotClient(_telegramOptions.Token);

        return await _exponentialBackOff.WithRetry(async () =>
        {
            var doc = await api.SendDocumentAsync(
                _telegramOptions.PublicChatId,
                fileId,
                disableNotification: true,
                caption: markdownV2.ToString(),
                parseMode: Constants.MarkdownV2,
                messageThreadId: _telegramOptions.PublicTopicId,
                replyMarkup: inlineKeyboardMarkup);

            return doc.MessageId;
        });
    }

    public async Task<bool> UpdateTextMessage(Markdown markdownV2, int messageId, InlineKeyboardMarkup? inlineKeyboardMarkup)
    {
        var api = new BotClient(_telegramOptions.Token);

        return await _exponentialBackOff.WithRetry(async () =>
        {
            try
            {
                await api.EditMessageTextAsync(new(markdownV2.ToString())
                {
                    ChatId = _telegramOptions.PublicChatId,
                    ParseMode = Constants.MarkdownV2,
                    MessageId = messageId,
                    ReplyMarkup = inlineKeyboardMarkup,
                    DisableWebPagePreview = true,
                });
            }
            catch (BotRequestException e) when (e.Message.Contains(
                                                    "Bad Request: message is not modified: specified new message content and reply markup are exactly the same as a current content and reply markup of the message"))
            {
                _logger.LogInformation($"message {messageId} is not modified. ok.");
            }
            catch (BotRequestException e) when (e.Message.Contains("message to edit not found"))
            {
                return false;
            }
            catch (BotRequestException e) when (e.Message.Contains("can't parse entities"))
            {
                _logger.LogError(e, "test was: {text}", markdownV2.ToString());
                throw;
            }

            return true;
        });
    }

    public async Task<bool> UpdateFileMessage(Markdown markdownV2, int messageId, InlineKeyboardMarkup? inlineKeyboardMarkup)
    {
        var api = new BotClient(_telegramOptions.Token);

        return await _exponentialBackOff.WithRetry(async () =>
        {
            try
            {
                await api.EditMessageCaptionAsync(
                    _telegramOptions.PublicChatId,
                    messageId,
                    markdownV2.ToString(),
                    Constants.MarkdownV2,
                    replyMarkup: inlineKeyboardMarkup);
            }
            catch (BotRequestException e) when (e.Message.Contains(
                                                    "Bad Request: message is not modified: specified new message content and reply markup are exactly the same as a current content and reply markup of the message"))
            {
                _logger.LogInformation($"message {messageId} is not modified. ok.");
            }
            catch (BotRequestException e) when (e.Message.Contains("message to edit not found"))
            {
                return false;
            }

            return true;
        });
    }

    public async Task Pin(int messageId)
    {
        var api = new BotClient(_telegramOptions.Token);

        await _exponentialBackOff.WithRetry(async () =>
        {
            await api.PinChatMessageAsync(
                _telegramOptions.PublicChatId,
                messageId,
                true);
        });
    }

    public async Task Unpin(int messageId)
    {
        var api = new BotClient(_telegramOptions.Token);

        await _exponentialBackOff.WithRetry(async () =>
        {
            await api.UnPinChatMessageAsync(
                _telegramOptions.PublicChatId,
                messageId);
        });
    }

    public async Task AdminPrivateMessageSafe(Markdown message)
    {
        try
        {
            var api = new BotClient(_telegramOptions.Token);

            await api.SendMessageAsync(_telegramOptions.AdminId, message.ToString(), parseMode: Constants.MarkdownV2);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Admin private message error.");
        }
    }

    public async Task AdminPrivateMessageSafe(string message) =>
        await AdminPrivateMessageSafe(message.ToMarkdown());

    public async Task UpdateCommands(List<CommandAttribute> commands)
    {
        var api = new BotClient(_telegramOptions.Token);

        await api.SetMyCommandsAsync(new SetMyCommandsArgs(commands.Where(x => x.SupportsNoParameters).Select(x => new BotCommand(x.Alias, x.ButtonDescription)), new BotCommandScopeChat(_telegramOptions.AdminId)));

        await api.SetMyCommandsAsync(new SetMyCommandsArgs(commands.Where(x => x.SupportsNoParameters).Where(x => x.AppliesToRegular).Select(x => new BotCommand(x.Alias, x.ButtonDescription)), new BotCommandScopeDefault()));
    }
}