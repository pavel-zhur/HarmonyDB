using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.Ios;
using Telegram.BotAPI;

namespace OneShelf.Telegram.Processor.Services;

public class Regeneration
{
    private readonly ILogger<Regeneration> _logger;
    private readonly ChannelActions _channelActions;
    private readonly SongsDatabase _songsDatabase;
    private readonly Io _io;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly SongsDatabaseMemory _songsDatabaseMemory;
    private readonly TelegramOptions _options;

    public Regeneration(ILogger<Regeneration> logger, ChannelActions channelActions, SongsDatabase songsDatabase, Io io, MessageMarkdownCombiner messageMarkdownCombiner, IHostApplicationLifetime hostApplicationLifetime, SongsDatabaseMemory songsDatabaseMemory, IOptions<TelegramOptions> options)
    {
        _logger = logger;
        _channelActions = channelActions;
        _songsDatabase = songsDatabase;
        _io = io;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _hostApplicationLifetime = hostApplicationLifetime;
        _songsDatabaseMemory = songsDatabaseMemory;
        _options = options.Value;
    }

    /// <summary>
    /// 1. Update all songs (update | create missing | file id nullability or value changed -> delete + create)
    /// 2. Update all artists (delete | update | create missing)
    /// 3. Update all user tops (delete | update | create missing)
    /// 4. Check if category parts changed -> DropLists
    /// 5. Update all category parts (update | create missing)
    /// 6. Update new (update | create missing)
    /// 7. Update top (update | create missing)
    /// 8. Update main (update | create missing + pin)
    /// </summary>
    public async Task UpdateAll()
    {
        await Update(
            MessageType.Song, 
            _messageMarkdownCombiner.Songs, 
            x => x.message, 
            x => x.songId, 
            x => x.SongId!.Value,
            (message, x) => message.SongId = x.songId,
            x => x.caption);

        await Update(
            MessageType.Artist,
            _messageMarkdownCombiner.Artists,
            x => x.message,
            x => x.artistId,
            x => x.ArtistId!.Value,
            (message, x) => message.ArtistId = x.artistId,
            x => x.title);

        await Update(
            MessageType.CategoryPart,
            _messageMarkdownCombiner.CategoryParts,
            x => x.message,
            x => (x.category, x.part),
            x => (x.Category, x.Part),
            (message, x) =>
            {
                message.Category = x.category;
                message.Part = x.part;
            },
            null,
            async () => await DropLists(MessageType.Main, MessageType.CategoryPart));

        await Update<MessageMarkup, int>(
            MessageType.Main,
            async () => new() { await _messageMarkdownCombiner.Main() },
            x => x,
            _ => 0,
            _ => 0,
            (_, _) => {});

        _songsDatabaseMemory.Advance();
    }

    private async Task Update<TItem, TKey>(
        MessageType messageType,
        Func<Task<List<TItem>>> itemsGetter,
        Func<TItem, MessageMarkup> messageMarkupSelector,
        Func<TItem, TKey> itemKeySelector,
        Func<Message, TKey> messageKeySelector,
        Action<Message, TItem>? messageKeySetter,
        Func<TItem, string>? logTitleSelector = null,
        Func<Task>? actionOnSetChange = null,
        bool isAnnouncementTopic = false,
        bool onlyUpdatePossibleAndForgiveCantEdit = false)
        where TKey : struct
    {
        if (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested) return;

        async Task<Dictionary<TKey, Message>> ReadMessagesByKey()
        {
            return (await _songsDatabase.Messages
                    .Where(x => x.TenantId == _options.TenantId)
                    .Where(x => x.Type == messageType)
                    .Include(x => x.Song)
                    .ThenInclude(x => x.Artists)
                    .Include(x => x.Artist)
                    .ToListAsync())
                .ToDictionary(messageKeySelector);
        }

        var items = await itemsGetter();

        var messagesByKey = await ReadMessagesByKey();
        var itemsByKey = items.WithIndices().ToDictionary(x => itemKeySelector(x.x));

        var keys = itemsByKey.Keys.Union(messagesByKey.Keys).ToList();
        if (keys.Count != itemsByKey.Count || itemsByKey.Count != messagesByKey.Count)
        {
            if (actionOnSetChange != null)
            {
                await actionOnSetChange!();
                messagesByKey = await ReadMessagesByKey();
            }
        }

        foreach (var key in keys.OrderBy(x => itemsByKey.TryGetValue(x, out var value) ? value.i : -1))
        {
            if (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested) return;

            var ((targetItem, i), target) = itemsByKey.TryGetValue(key, out var value) ? (value, messageMarkupSelector(value.x)) : (default, null);
            var existing = messagesByKey.TryGetValue(key, out var value2) ? value2 : null;

            var logTitle = logTitleSelector?.Invoke(targetItem);

            if (target == null && existing == null) throw new("Couldn't have happened.");

            if (existing != null && (
                    target == null // need to delete because target is null
                    || existing.FileId != target.FileId)) // need to delete because the type is wrong or the file is wrong
            {
                if (onlyUpdatePossibleAndForgiveCantEdit) continue;

                if (await _channelActions.Delete(existing.MessageId))
                {
                    _songsDatabase.Messages.Remove(existing);
                    await _songsDatabase.SaveChangesAsyncX();

                    _io.WriteLine($"Dropped message {existing.GetCaption(logTitle)}.");

                    existing = null;
                }
                else
                {
                    _io.WriteLine($"Message couldn't be dropped {existing.GetCaption(logTitle)}.");

                    if (existing.FileId != target?.FileId)
                    {
                        throw new("Manual deletion needed.");
                    }
                }
            }

            if (target == null) continue; // either deleted or couldn't be deleted and it's ok

            var targetHash = JsonConvert.SerializeObject((target, Constants.MessageVersions[messageType]));
            if (existing != null)
            {
                // we know the update is sufficient
                if (targetHash == existing.Hash)
                {
                    // hash hasn't changed
                    continue;
                }

                bool result;
                try
                {
                    if (existing.FileId != null)
                    {
                        // we know it is a file
                        result = await _channelActions.UpdateFileMessage(target.BodyOrCaption, existing.MessageId,
                            target.InlineKeyboardMarkup);
                    }
                    else
                    {
                        // we know it is a text
                        result = await _channelActions.UpdateTextMessage(target.BodyOrCaption, existing.MessageId,
                            target.InlineKeyboardMarkup);
                    }
                }
                catch (BotRequestException e) when (onlyUpdatePossibleAndForgiveCantEdit && e.Message.Contains("message can't be edited"))
                {
                    // for some reason, there had been the deletion logic here previously
                    _logger.LogWarning(e, "Couldn't edit the message {} and forgiving.", existing.MessageId);
                    result = true;
                }
                catch (Exception ex)
                {
                    throw new($"Update failed for {messageType} {key}.", ex);
                }

                if (result)
                {
                    existing.Hash = targetHash;
                    await _songsDatabase.SaveChangesAsyncX();

                    _io.WriteLine($"Updated message {existing.GetCaption(logTitle)}.");

                    continue;
                }

                _songsDatabase.Messages.Remove(existing);
                await _songsDatabase.SaveChangesAsyncX();
            }

            if (onlyUpdatePossibleAndForgiveCantEdit) continue;

            int messageId;
            try
            {
                messageId = target.FileId != null
                ? await _channelActions.PublishFileMessage(target.FileId, target.BodyOrCaption, target.InlineKeyboardMarkup)
                : await _channelActions.PublishTextMessage(target.BodyOrCaption, target.InlineKeyboardMarkup, isAnnouncementTopic);
            }
            catch (Exception ex)
            {
                throw new($"Publish failed for {messageType} {key} {logTitle}.", ex);
            }

            var message = new Message
            {
                TenantId = _options.TenantId,
                FileId = target.FileId,
                MessageId = messageId,
                Type = messageType,
                Hash = targetHash,
            };

            messageKeySetter(message, targetItem!);
            _songsDatabase.Messages.Add(message);
            await _songsDatabase.SaveChangesAsyncX();

            if (target.Pin)
            {
                await _channelActions.Pin(messageId);
            }

            _io.WriteLine($"Published message {message.GetCaption(logTitle)}.");
        }
    }

    public async Task DropLists(params MessageType[] typesToDrop)
    {
        foreach (var existingMessage in await _songsDatabase.Messages
                     .Where(x => x.TenantId == _options.TenantId)
                     .Where(m => typesToDrop.Contains(m.Type))
                     .ToListAsync())
        {
            if (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested) break;

            if (!await _channelActions.Delete(existingMessage.MessageId))
            {
                throw new("Message deletion failed. Manual help needed.");
            }

            _songsDatabase.Messages.Remove(existingMessage);
            await _songsDatabase.SaveChangesAsyncX();
        }
    }
}