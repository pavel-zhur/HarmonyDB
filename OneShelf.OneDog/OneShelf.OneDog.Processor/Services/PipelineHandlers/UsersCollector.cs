using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Services.Base;
using OneShelf.Telegram.PipelineHandlers;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public class UsersCollector : UsersCollectorBase
{
    private readonly ILogger<UsersCollector> _logger;
    private readonly DogDatabase _dogDatabase;

    public UsersCollector(ILogger<UsersCollector> logger, DogDatabase dogDatabase, IScopedAbstractions scopedAbstractions) 
        : base(scopedAbstractions)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
    }

    protected override async Task Handle(List<(long Id, string? FirstName, string? LastName, string? Username, string? LanguageCode, string Title)> users)
    {
        var ids = users.Select(x => x.Id).ToList();

        var usersById = await _dogDatabase.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        foreach (var (id, _, _, _, _, title) in users)
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
    }
}