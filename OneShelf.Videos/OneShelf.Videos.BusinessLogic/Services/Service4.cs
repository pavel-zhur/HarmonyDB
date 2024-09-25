using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Nito.AsyncEx;
using OneShelf.Videos.BusinessLogic.Models;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models.Live;
using TL;
using WTelegram;
using Document = TL.Document;

namespace OneShelf.Videos.BusinessLogic.Services;

public class Service4(IOptions<VideosOptions> options, ILogger<Service4> logger, VideosDatabase videosDatabase, TelegramLoggerInitializer _) : IDisposable
{
    private byte[]? _session;
    private readonly AsyncLock _databaseLock = new();

    public async Task Try()
    {
        await using var client = await Login();

        var allChats = await client.Messages_GetAllChats();

        foreach (var chatId in options.Value.TelegramChatIds)
        {
            var chat = allChats.chats[chatId];

            await ProcessChat(client, chat);
        }
    }

    private async Task ProcessChat(Client client, ChatBase chat)
    {
        var inputPeer = chat.ToInputPeer();

        var messages = await ReadAllMessages(client, inputPeer);
        // await File.WriteAllTextAsync("all.json", JsonConvert.SerializeObject(messages, Formatting.Indented));

        await LoadStories(client, inputPeer, messages);

        var topics = await GetTopicsAndMedia(messages);

        //await client.DownloadFileAsync(topics[0].media[0].document.ToFileLocation(topics[0].media[0].document.LargestThumbSize), outputStream)

        await Save(chat, topics);

        await Download(client, chat, topics);
    }

    private async Task Download(Client client, ChatBase chat, Dictionary<int, (string name, List<(Document? document, Photo? photo, Message message, string mediaFlags)> media)> topics)
    {
        var liveChat = await videosDatabase.LiveChats.Include(x => x.LiveTopics).ThenInclude(x => x.LiveMediae).Where(x => x.Id == chat.ID).SingleAsync();
        var liveMediae = liveChat.LiveTopics.SelectMany(t => t.LiveMediae).Join(topics.SelectMany(x => x.Value.media), x => x.Id, x => x.message.ID, (x, y) => (x, y)).ToList();
        var downloadedItems = await videosDatabase.DownloadedItems.Where(x => videosDatabase.LiveMediae.Any(y => y.MediaId == x.LiveMediaId)).ToDictionaryAsync(x => x.LiveMediaId);

        var success = 0;
        var failed = 0;
        var items = liveMediae.DistinctBy(x => x.x.MediaId).Where(x => !downloadedItems.TryGetValue(x.x.MediaId, out var y) || y.ThumbnailFileName == null).ToList();

        await Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = 3, },
            async (item, cancellationToken) =>
            {
                var (liveMedia, (document, photo, message, mediaFlags)) = item;
                var downloaded = downloadedItems.GetValueOrDefault(liveMedia.MediaId);

                if (downloaded == null)
                {
                    try
                    {
                        using var outputStream = new MemoryStream();
                        if (document != null)
                        {
                            await client.DownloadFileAsync(document, outputStream);
                        }
                        else
                        {
                            await client.DownloadFileAsync(photo, outputStream);
                        }

                        var fileName = liveMedia.FileName != null && !string.IsNullOrWhiteSpace(Path.GetExtension(liveMedia.FileName)) ? $"{liveMedia.MediaId}{Path.GetExtension(liveMedia.FileName)}" : liveMedia.MediaId.ToString();
                        await File.WriteAllBytesAsync(
                            Path.Combine(options.Value.BasePath, "_instant", fileName),
                            outputStream.ToArray(), cancellationToken);

                        using var _ = await _databaseLock.LockAsync();

                        videosDatabase.DownloadedItems.Add(downloaded = new()
                        {
                            LiveMediaId = liveMedia.MediaId,
                            FileName = fileName,
                        });

                        await videosDatabase.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Interlocked.Increment(ref failed);
                        logger.LogError(e, message: "Error downloading an item.");
                        downloaded = null;
                    }
                }

                if (downloaded != null && (document?.LargestThumbSize != null || photo != null))
                {
                    // download thumbnail too
                    try
                    {
                        using var outputStream = new MemoryStream();
                        await client.DownloadFileAsync((InputFileLocationBase?)document?.ToFileLocation(document.LargestThumbSize) ?? photo!.ToFileLocation(photo.sizes.SingleOrDefault(x => x.Type == "x") ?? photo.sizes.MaxBy(x => x.FileSize)), outputStream);
                        var fileName = $"{liveMedia.MediaId}.jpg";
                        await File.WriteAllBytesAsync(
                            Path.Combine(options.Value.BasePath, "_instant", "thumbnails", fileName),
                            outputStream.ToArray(), cancellationToken);

                        using var _ = await _databaseLock.LockAsync();
                        downloaded.ThumbnailFileName = fileName;
                        await videosDatabase.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Interlocked.Increment(ref failed);
                        logger.LogError(e, message: "Error downloading an item.");
                    }
                }

                logger.LogInformation("Done {value} / {total}, failed {failed}", Interlocked.Increment(ref success), items.Count, failed);
            });
    }

    private async Task Save(ChatBase chat, Dictionary<int, (string name, List<(Document? document, Photo? photo, Message message, string mediaFlags)> media)> topics)
    {
        var liveChat = await videosDatabase.LiveChats.Include(x => x.LiveTopics).ThenInclude(x => x.LiveMediae).SingleOrDefaultAsync(x => x.Id == chat.ID);
        if (liveChat == null)
        {
            liveChat = new()
            {
                Id = chat.ID,
                LiveTopics = new List<LiveTopic>(),
            };

            videosDatabase.LiveChats.Add(liveChat);
        }

        liveChat.Title = chat.Title;

        foreach (var topic in topics)
        {
            var liveTopic = liveChat.LiveTopics.SingleOrDefault(x => x.Id == topic.Key);
            if (liveTopic == null)
            {
                liveTopic = new()
                {
                    Id = topic.Key,
                    LiveMediae = new List<LiveMedia>(),
                };

                liveChat.LiveTopics.Add(liveTopic);
            }

            liveTopic.Title = topic.Value.name;

            var liveMediae = liveTopic.LiveMediae.ToDictionary(x => x.Id);
            foreach (var media in topic.Value.media)
            {
                var liveMedia = liveMediae.GetValueOrDefault(media.message.ID);
                if (liveMedia == null)
                {
                    liveMedia = new()
                    {
                        Id = media.message.ID,
                    };

                    liveTopic.LiveMediae.Add(liveMedia);
                }

                liveMedia.MessageDate = media.message.fwd_from?.date ?? media.message.Date;
                liveMedia.IsForwarded = media.message.fwd_from != null;
                liveMedia.MediaType = media.message.media.GetType().ToString();
                liveMedia.MediaFlags = media.mediaFlags;

                if (media.document is {} document)
                {
                    liveMedia.Type = LiveMediaType.Document;
                    liveMedia.MediaDate = document.date;
                    liveMedia.MediaId = document.ID;
                    liveMedia.FileName = document.Filename;
                    liveMedia.Flags = document.flags.ToString();
                    liveMedia.MimeType = document.mime_type;
                    liveMedia.Size = document.size;
                    liveMedia.DocumentAttributes = JsonConvert.SerializeObject(document.attributes);
                    liveMedia.DocumentAttributeTypes = string.Join(", ", document.attributes.Select(a => a.GetType()));

                    var attributeVideo = document.attributes.OfType<DocumentAttributeVideo>().SingleOrDefault();
                    if (attributeVideo != null)
                    {
                        liveMedia.Width = attributeVideo.w;
                        liveMedia.Height = attributeVideo.h;
                        liveMedia.Duration = attributeVideo.duration;
                        liveMedia.VideoFlags = attributeVideo.flags.ToString();
                    }
                }
                else if (media.photo is {} photo)
                {
                    liveMedia.Type = LiveMediaType.Photo;
                    liveMedia.MediaDate = photo.date;
                    liveMedia.MediaId = photo.ID;
                    liveMedia.Flags = photo.flags.ToString();
                    liveMedia.Size = photo.LargestPhotoSize.FileSize;
                    liveMedia.Width = photo.LargestPhotoSize.Width;
                    liveMedia.Height = photo.LargestPhotoSize.Height;
                }
                else
                {
                    throw new("A media should either be a document or a photo.");
                }
            }
        }

        await videosDatabase.SaveChangesAsync();
    }

    private async Task LoadStories(Client client, InputPeer inputPeer, List<MessageBase> messages)
    {
        foreach (var message in messages)
        {
            if (message is Message { media: MessageMediaStory { story: null } story })
            {
                story.story = (await client.Stories_GetStoriesByID(new InputPeerUserFromMessage { peer = inputPeer, msg_id = message.ID, user_id = story.peer.ID }, story.id)).stories.Single();
            }
        }
    }

    private static async Task<Dictionary<int, (string name, List<(Document? document, Photo? photo, Message message, string mediaFlags)> media)>> GetTopicsAndMedia(List<MessageBase> messages)
    {
        var topics = new Dictionary<int, (string name, List<(Document? document, Photo? photo, Message message, string mediaFlags)> media)>
        {
            { 0, ("General", new()) },
        };

        foreach (var messageBase in messages.OrderBy(x => x.ID))
        {
            // getting topic id
            int? topicId = null;
            if (messageBase.ReplyTo is MessageReplyHeader { flags: MessageReplyHeader.Flags flags } header)
            {
                if (flags.HasFlag(MessageReplyHeader.Flags.has_reply_to_top_id))
                {
                    topicId = header.reply_to_top_id;
                }
                else if (flags.HasFlag(MessageReplyHeader.Flags.has_reply_to_msg_id))
                {
                    topicId = header.reply_to_msg_id;
                }
            }

            if (topicId.HasValue && !topics.ContainsKey(topicId.Value)) topicId = null;

            // handling the topics collection
            if (messageBase is MessageService { action: MessageActionTopicCreate messageActionTopicCreate })
            {
                topics.Add(messageBase.ID, (messageActionTopicCreate.title, new()));
            }

            if (messageBase is MessageService { action: MessageActionTopicEdit messageActionTopicEdit })
            {
                topics[topicId!.Value] = (messageActionTopicEdit.title, topics[topicId.Value].media);
            }

            (Document? document, Photo? photo, Message message, string mediaFlags)? media = messageBase switch
            {
                Message { media: MessageMediaDocument { document: Document document, flags: { } mediaFlags } } message => (document, null, message, mediaFlags.ToString()),
                Message { media: MessageMediaPhoto { photo: Photo photo, flags: { } mediaFlags } } message => (null, photo, message, mediaFlags.ToString()),
                Message { media: MessageMediaStory { story: StoryItem { media: MessageMediaDocument { document: Document document, flags: { } mediaFlags } } } } message => (document, null, message, mediaFlags.ToString()),
                Message { media: MessageMediaStory { story: StoryItem { media: MessageMediaPhoto { photo: Photo photo, flags: { } mediaFlags } } } } message => (null, photo, message, mediaFlags.ToString()),
                _ => default,
            };

            if (media.HasValue)
                topics[topicId ?? 0].media.Add(media.Value);
        }

        return topics;
    }

    private static async Task<List<MessageBase>> ReadAllMessages(Client client, InputPeer inputPeer)
    {
        var offsetId = 0;
        List<MessageBase> messages = new();
        int? count = null;
        while (true)
        {
            var z = await client.Messages_GetHistory(inputPeer, offset_id: offsetId);
            if (!z.Messages.Any()) break;
            count ??= z.Count;
            messages.AddRange(z.Messages);
            offsetId = z.Messages.Min(x => x.ID);
        }

        if (messages.Count != count) throw new("Could not read all messages.");
        return messages;
    }

    private async Task<Client> Login()
    {
        Client? client = null;
        try
        {
            client = new(
                what => what switch
                {
                    "api_id" => options.Value.TelegramApiId,
                    "api_hash" => options.Value.TelegramApiHash,
                    "phone_number" => options.Value.TelegramPhoneNumber,
                    "verification_code" => null,
                    "password" => "secret!", // if user has enabled 2FA
                    _ => null, // let WTelegramClient decide the default config
                },
                File.Exists(options.Value.TelegramAuthPath)
                    ? await File.ReadAllBytesAsync(options.Value.TelegramAuthPath)
                    : null,
                x => _session = x);
        
            var myself = await client.LoginUserIfNeeded();

            logger.LogInformation("We are logged-in as {myself} (id {id})", myself, myself.id);
            return client;
        }
        catch
        {
            if (client != null) await client.DisposeAsync();
            throw;
        }
    }

    public void Dispose()
    {
        if (_session != null)
        {
            File.WriteAllBytes(options.Value.TelegramAuthPath, _session);
            logger.LogInformation("Successfully disposed the telegram client session.");
        }
    }
}