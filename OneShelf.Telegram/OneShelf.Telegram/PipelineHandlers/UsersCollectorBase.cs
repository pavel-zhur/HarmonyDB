using OneShelf.Common;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.PipelineHandlers;

public abstract class UsersCollectorBase : PipelineHandler
{
    public UsersCollectorBase(IScopedAbstractions scopedAbstractions) : base(scopedAbstractions)
    {
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
            .Select(x => (x!.Id, FirstName: (string?)x.FirstName, x.LastName, x.Username, x.LanguageCode))
            .Concat((update.Message?.UsersShared?.Users ?? [])
                .Select(x => (Id: x.UserId, x.FirstName, x.LastName, x.Username, LanguageCode: (string?)null)))
            .GroupBy(x => x.Id)
            .Select(g => (
                Id: g.Key,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.FirstName)).FirstName,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.LastName)).LastName,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Username)).Username,
                g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.LanguageCode)).LanguageCode))
            .Select(u => (
                u.Id,
                u.FirstName,
                u.LastName,
                u.Username,
                u.LanguageCode,
                Title: string
                    .Join(" ", new[]
                    {
                        u.FirstName,
                        u.LastName
                    }.Where(x => !string.IsNullOrWhiteSpace(x)))
                    .SelectSingle(x => string.IsNullOrWhiteSpace(x) ? u.Username : x)
                    .SelectSingle(x => string.IsNullOrWhiteSpace(x) ? u.Id.ToString() : x)))
            .ToList();

        await Handle(users);
        return false;
    }

    protected abstract Task Handle(List<(long Id, string? FirstName, string? LastName, string? Username, string? LanguageCode, string Title)> users);
}