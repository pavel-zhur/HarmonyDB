using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Telegram.PipelineHandlers;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class UsersCollector : UsersCollectorBase
{
    private readonly ILogger<UsersCollector> _logger;
    private readonly SongsDatabase _songsDatabase;

    public UsersCollector(ILogger<UsersCollector> logger, SongsDatabase songsDatabase, IScopedAbstractions scopedAbstractions) 
        : base(scopedAbstractions)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
    }

    protected override async Task Handle(List<(long Id, string? FirstName, string? LastName, string? Username, string? LanguageCode, string Title)> users)
    {
        var ids = users.Select(x => x.Id).ToList();

        var usersById = await _songsDatabase.Users
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

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
                    Tenant = new()
                    {
                        PrivateDescription = Tenant.PersonalPrivateDescription(id),
                    }
                };

                _songsDatabase.Users.Add(user);
            }
        }

        await _songsDatabase.SaveChangesAsyncX();
    }
}