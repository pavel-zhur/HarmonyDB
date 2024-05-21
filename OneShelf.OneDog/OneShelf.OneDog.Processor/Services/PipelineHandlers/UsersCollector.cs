using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Helpers;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public class UsersCollector : PipelineHandler
{
    private readonly ILogger<UsersCollector> _logger;

    public UsersCollector(IOptions<TelegramOptions> telegramOptions, ILogger<UsersCollector> logger, DogDatabase dogDatabase, ScopeAwareness scopeAwareness) 
        : base(telegramOptions, dogDatabase, scopeAwareness)
    {
        _logger = logger;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        var users = new[]
            {
                update.Message?.From,
                update.Message?.ForwardFrom,
                update.Message?.ReplyToMessage?.From,
                update.Message?.ReplyToMessage?.ForwardFrom,
                update.InlineQuery?.From,
                update.CallbackQuery?.From,
                update.CallbackQuery?.Message?.From,
                update.CallbackQuery?.Message?.ForwardFrom,
                update.CallbackQuery?.Message?.ReplyToMessage?.From,
                update.CallbackQuery?.Message?.ReplyToMessage?.ForwardFrom,
            }
            .Where(x => x != null)
            .Select(x => (x.Id, x.FirstName, x.LastName, x.Username))
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .Select(u => (
                u.Id,
                title: u.GetUserTitle()))
            .ToList();

        var ids = users.Select(x => x.Id).ToList();

        var usersById = await DogDatabase.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        foreach (var (id, title) in users)
        {
            if (usersById.TryGetValue(id, out var user))
            {
                user.Title = title;
            }
            else
            {
                user = new()
                {
                    Id = id,
                    Title = title,
                    CreatedOn = DateTime.Now,
                };

                DogDatabase.Users.Add(user);
            }
        }

        await DogDatabase.SaveChangesAsync();

        return false;
    }
}