using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public class UsersCollector : PipelineHandler
{
    private readonly ILogger<UsersCollector> _logger;
    private readonly DogDatabase _dogDatabase;

    public UsersCollector(ILogger<UsersCollector> logger, DogDatabase dogDatabase, IScopedAbstractions scopedAbstractions) 
        : base(scopedAbstractions)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        var users = new[]
            {
                update.Message?.From,
                update.Message?.ReplyToMessage?.From,
                update.InlineQuery?.From,
                update.CallbackQuery?.From,
            }
            .Where(x => x != null)
            .Select(x => (x.Id, x.FirstName, x.LastName, x.Username))
            .Concat((update.Message?.UsersShared?.Users ?? Enumerable.Empty<SharedUser>())
                .Select(x => (Id: x.UserId, x.FirstName, x.LastName, x.Username)))
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .Select(u => (
                u.Id,
                title: u.GetUserTitle()))
            .ToList();

        var ids = users.Select(x => x.Id).ToList();

        var usersById = await _dogDatabase.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

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

                _dogDatabase.Users.Add(user);
            }
        }

        await _dogDatabase.SaveChangesAsync();

        return false;
    }
}