using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Services.Commands.Base;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_aw", "Authorize Web")]
public class AuthorizeWebForLastOwnChatter : Command
{
    private readonly SongsDatabase _songsDatabase;

    public AuthorizeWebForLastOwnChatter(Io io, SongsDatabase songsDatabase, IOptions<TelegramOptions> options) : base(io, options)
    {
        _songsDatabase = songsDatabase;
    }

    protected override async Task ExecuteQuickly()
    {
        var choice = Io.StrictChoice<Confirmation>("Речь о шкафе (да) или об иллюстрациях (не совсем)?");

        bool Get(User user) => choice == Confirmation.Yes
            ? user.TenantId == Options.TenantId : user.IsAuthorizedToUseIllustrations;

        void Set(User user)
        {
            if (choice == Confirmation.Yes)
                user.TenantId = Options.TenantId;
            else
                user.IsAuthorizedToUseIllustrations = true;
        }

        var last = await _songsDatabase.Interactions
            .Where(x => x.InteractionType == InteractionType.OwnChatterMessage)
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedOn)
            .FirstAsync();

        if (!Get(last.User))
        {
            if (Io.StrictChoice<Confirmation>($"Последний разговор с собакой: {last.UserId} {last.User.Title}, Разрешить?") == Confirmation.Yes)
            {
                Set(last.User);
                await _songsDatabase.SaveChangesAsyncX();
                return;
            }
        }

        var also = await _songsDatabase.Interactions
            .Where(x => x.InteractionType == InteractionType.AskWeb && x.Serialized == Options.TenantId.ToString())
            .GroupBy(x => x.User)
            .Select(g => new
            {
                User = g.Key,
                LastAsked = g.Max(x => x.CreatedOn),
                FirstAsked = g.Min(x => x.CreatedOn),
            })
            .OrderByDescending(g => g.LastAsked)
            .Take(10)
            .ToListAsync();

        also = also.Where(x => !Get(x.User)).ToList();

        Io.WriteLine(string.Join(Environment.NewLine, also.WithIndices().Select(x => $"{x.i}. {x.x.User.Id} {x.x.User.Title} {x.x.FirstAsked} {x.x.LastAsked}")));
        var index = Io.FreeChoiceInt("Кому дать? /start для выхода.");

        Set(also[index].User);
        await _songsDatabase.SaveChangesAsyncX();
    }
}