using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Model;
using OneShelf.OneDog.Processor.Services.PipelineHandlers.Base;
using OneShelf.Telegram.Model;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public class ChatsCollector : PipelineHandler
{
    private readonly ILogger<ChatsCollector> _logger;
    private readonly DogDatabase _dogDatabase;

    public ChatsCollector(IOptions<TelegramOptions> telegramOptions, ILogger<ChatsCollector> logger, DogDatabase dogDatabase, TelegramContext telegramContext) 
        : base(telegramOptions, telegramContext)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        var chat = update.Message?.Chat
                   ?? update.MyChatMember?.Chat
                   ?? update.ChatJoinRequest?.Chat
                   ?? update.ChannelPost?.Chat
                   ?? update.ChatMember?.Chat;

        if (chat == null) return false;

        var dbChat = await _dogDatabase.Chats.SingleOrDefaultAsync(x => x.ChatId == chat.Id && x.DomainId == TelegramContext.DomainId);
        if (dbChat == null)
        {
            dbChat = new()
            {
                ChatId = chat.Id,
                DomainId = TelegramContext.DomainId,
                FirstUpdateReceivedOn = DateTime.Now,
                LastUpdateReceivedOn = DateTime.Now,
                UpdatesCount = 0,
                Type = chat.Type,
            };

            _dogDatabase.Chats.Add(dbChat);
        }

        dbChat.UpdatesCount++;
        dbChat.LastUpdateReceivedOn = DateTime.Now;
        dbChat.Title = chat.Title;
        dbChat.Type = chat.Type;
        dbChat.IsForum = chat.IsForum;
        await _dogDatabase.SaveChangesAsync();

        return false;
    }
}