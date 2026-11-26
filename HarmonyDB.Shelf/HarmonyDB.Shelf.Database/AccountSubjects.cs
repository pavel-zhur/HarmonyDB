using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Database;

/// <summary>
/// The names a person is known by to the sources (the source contract's subjects): opaque to them, compared as strings.
/// The first is the person's name now: <c>account:</c> and the account's id. Then the accounts once merged into it; then,
/// while the old shelf lives, each of the person's Telegram sign-ins, <c>telegram:</c> and its id, as the old shelf's
/// own chords name their authors.
/// </summary>
public static class AccountSubjects
{
    public static string Of(Guid accountId) => $"account:{accountId:D}";

    public static string OfTelegram(string telegramUserId) => $"{TelegramPrefix}{telegramUserId}";


    private const string TelegramPrefix = "telegram:";

    public static async Task<IReadOnlyList<string>> LoadAsync(ShelfDbContext db, Guid accountId, CancellationToken cancellationToken)
    {
        var merged = await db.AccountMerges.AsNoTracking().Where(x => x.IntoId == accountId)
            .OrderBy(x => x.MergedAt).Select(x => x.MergedId).ToListAsync(cancellationToken);
        var telegram = await db.AccountLogins.AsNoTracking().Where(x => x.AccountId == accountId && x.Provider == "telegram")
            .OrderBy(x => x.CreatedAt).Select(x => x.Subject).ToListAsync(cancellationToken);

        return merged.Select(Of).Prepend(Of(accountId)).Concat(telegram.Select(OfTelegram)).Distinct(StringComparer.Ordinal).ToList();
    }
}