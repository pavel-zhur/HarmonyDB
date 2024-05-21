using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public class ChatsCollector : PipelineHandler
{
    private readonly ILogger<ChatsCollector> _logger;

    public ChatsCollector(IOptions<TelegramOptions> telegramOptions, ILogger<ChatsCollector> logger, DogDatabase dogDatabase, ScopeAwareness scopeAwareness) 
        : base(telegramOptions, dogDatabase, scopeAwareness)
    {
        _logger = logger;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        var chat = update.Message?.Chat
                   ?? update.MyChatMember?.Chat
                   ?? update.ChatJoinRequest?.Chat
                   ?? update.ChannelPost?.Chat
                   ?? update.ChatMember?.Chat;

        if (chat == null) return false;

        var dbChat = await DogDatabase.Chats.SingleOrDefaultAsync(x => x.ChatId == chat.Id && x.DomainId == ScopeAwareness.DomainId);
        if (dbChat == null)
        {
            dbChat = new()
            {
                ChatId = chat.Id,
                DomainId = ScopeAwareness.DomainId,
                FirstUpdateReceivedOn = DateTime.Now,
                LastUpdateReceivedOn = DateTime.Now,
                UpdatesCount = 0,
                Type = chat.Type,
            };

            DogDatabase.Chats.Add(dbChat);
        }

        dbChat.UpdatesCount++;
        dbChat.LastUpdateReceivedOn = DateTime.Now;
        dbChat.Title = chat.Title;
        dbChat.Type = chat.Type;
        dbChat.IsForum = chat.IsForum;
        await DogDatabase.SaveChangesAsync();

        return false;
    }
}