using Microsoft.EntityFrameworkCore;
using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Processor.Services;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDragon.Processor.PipelineHandlers;

public class ChatsCollector : PipelineHandler
{
    private readonly DragonDatabase _dragonDatabase;
    private readonly DragonScope _scope;

    public ChatsCollector(IScopedAbstractions scopedAbstractions, DragonDatabase dragonDatabase, DragonScope scope) 
        : base(scopedAbstractions)
    {
        _dragonDatabase = dragonDatabase;
        _scope = scope;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        var chats = new[]
            {
                update.ChannelPost?.Chat,
                update.ChatBoost?.Chat,
                update.ChatJoinRequest?.Chat,
                update.ChatMember?.Chat,
                update.MyChatMember?.Chat,
                update.EditedChannelPost?.Chat,
                update.RemovedChatBoost?.Chat,
                update.PollAnswer?.VoterChat,
                update.Message?.Chat,
                update.Message?.SenderChat,
                update.Message?.ReplyToMessage?.Chat,
                update.Message?.ReplyToMessage?.SenderChat,
                update.CallbackQuery?.Message?.Chat,
                update.EditedMessage?.Chat,
                update.EditedMessage?.SenderChat,
                update.MessageReaction?.ActorChat,
                update.MessageReaction?.Chat,
            }
            .Where(x => x != null)
            .GroupBy(x => x!.Id)
            .Select(g => (
                Id: g.Key,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x!.FirstName))?.FirstName,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x!.LastName))?.LastName,
                g.FirstOrDefault(x => x!.IsForum.HasValue)?.IsForum,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x!.Title))?.Title,
                g.First()!.Type,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x!.Username))?.Username))
            .ToList();

        await Handle(chats);
        return false;
    }

    private async Task Handle(List<(long Id, string? FirstName, string? LastName, bool? IsForum, string? Title, string Type, string? Username)> chats)
    {
        var ids = chats.Select(x => x.Id).ToList();
        var existing = await _dragonDatabase.Chats.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(x => x.Id);

        foreach (var (id, firstName, lastName, isForum, title, type, username) in chats)
        {
            var chat = existing.GetValueOrDefault(id);
            if (chat == null)
            {
                chat = new()
                {
                    CreatedOn = DateTime.Now,
                    Id = id,
                    Type = type,
                };

                _dragonDatabase.Chats.Add(chat);
            }

            chat.Title = title ?? chat.Title;
            chat.FirstName = firstName;
            chat.LastName = lastName;
            chat.UserName = username;
            chat.IsForum = isForum;
        }

        await _dragonDatabase.SaveChangesAsync();
    }
}