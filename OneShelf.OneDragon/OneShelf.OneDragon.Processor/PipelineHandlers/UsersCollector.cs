using Microsoft.EntityFrameworkCore;
using OneShelf.OneDragon.Database;
using OneShelf.Telegram.PipelineHandlers;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDragon.Processor.PipelineHandlers;

public class UsersCollector : UsersCollectorBase
{
    private readonly DragonDatabase _dragonDatabase;

    public UsersCollector(IScopedAbstractions scopedAbstractions, DragonDatabase dragonDatabase)
        : base(scopedAbstractions)
    {
        _dragonDatabase = dragonDatabase;
    }

    protected override async Task Handle(List<(long Id, string? FirstName, string? LastName, string? Username, string? LanguageCode, string Title)> users)
    {
        var ids = users.Select(x => x.Id).ToList();
        var existing = await _dragonDatabase.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(x => x.Id);

        foreach (var (id, firstName, lastName, username, languageCode, title) in users)
        {
            var user = existing.GetValueOrDefault(id);
            if (user == null)
            {
                user = new()
                {
                    CreatedOn = DateTime.Now,
                    Id = id,
                    Title = title,
                    UseLimits = true,
                };

                _dragonDatabase.Users.Add(user);
            }

            user.Title = title;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.UserName = username;
            user.LanguageCode = languageCode;
        }

        await _dragonDatabase.SaveChangesAsync();
    }
}