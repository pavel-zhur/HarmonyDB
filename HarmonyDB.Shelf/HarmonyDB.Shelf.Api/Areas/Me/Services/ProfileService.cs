using HarmonyDB.Shelf.Api.Areas.App.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Me;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Me.Services;

/// <summary>
/// The person's profile, settings and sign-ins.
/// </summary>
public sealed class ProfileService(ShelfDbContext db, TimeProvider time)
{
    public const double MinScale = 0.25;
    public const double MaxScale = 4;

    public async Task<Profile> GetAsync(Guid accountId, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
        => await db.Accounts.AsNoTracking().Where(x => x.Id == accountId)
            .Select(x => new Profile
            {
                Id = x.Id,
                DisplayName = x.DisplayName,
                PictureUrl = x.PictureUrl,
                CreatedAt = x.CreatedAt,
                Permissions = permissions,
            })
            .SingleAsync(cancellationToken);

    public async Task<Settings> GetSettingsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().Include(x => x.Settings).SingleAsync(x => x.Id == accountId, cancellationToken);
        return ToModel(account.Locale, account.Settings ?? new AccountSettings());
    }

    public async Task<Settings> PutSettingsAsync(Guid accountId, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Locale != null && !PublicConfigurationService.Locales.Contains(settings.Locale))
            throw Invalid(nameof(Settings.Locale));
        if (!IsScale(settings.ChordsScale))
            throw Invalid(nameof(Settings.ChordsScale));
        if (!IsScale(settings.InterfaceScale))
            throw Invalid(nameof(Settings.InterfaceScale));

        var account = await db.Accounts.Include(x => x.Settings).SingleAsync(x => x.Id == accountId, cancellationToken);
        var row = account.Settings ??= new AccountSettings { AccountId = accountId };
        account.Locale = settings.Locale;
        row.ChordsScale = settings.ChordsScale;
        row.InterfaceScale = settings.InterfaceScale;
        row.LessMode = settings.LessMode;
        row.Accidentals = settings.Accidentals switch
        {
            Accidentals.AsWritten => AccidentalsMode.AsWritten,
            Accidentals.Flats => AccidentalsMode.Flats,
            Accidentals.Sharps => AccidentalsMode.Sharps,
            _ => throw Invalid(nameof(Settings.Accidentals)),
        };
        row.ShowDegrees = settings.ShowDegrees;
        row.ChordSuperscripts = settings.ChordSuperscripts;
        row.Theme = settings.Theme switch
        {
            Theme.System => ThemeMode.System,
            Theme.Light => ThemeMode.Light,
            Theme.Dark => ThemeMode.Dark,
            _ => throw Invalid(nameof(Settings.Theme)),
        };
        row.UpdatedAt = time.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return ToModel(account.Locale, row);
    }

    public async Task<IReadOnlyList<LoginItem>> ListLoginsAsync(Guid accountId, CancellationToken cancellationToken)
        => await db.AccountLogins.AsNoTracking().Where(x => x.AccountId == accountId).OrderBy(x => x.CreatedAt)
            .Select(x => new LoginItem
            {
                Id = x.Id,
                Provider = x.Provider,
                DisplayName = x.DisplayName,
                Username = x.Username,
                Email = x.Email,
                PictureUrl = x.PictureUrl,
                CreatedAt = x.CreatedAt,
                LastSignInAt = x.LastSignInAt,
            })
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Removes a sign-in of the account; never the last one.
    /// </summary>
    public async Task RemoveLoginAsync(Guid accountId, Guid loginId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The account's sign-ins locked: two removals at once cannot leave none.
        var logins = await db.AccountLogins.FromSql($"SELECT * FROM account_logins WHERE account_id = {accountId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var login = logins.SingleOrDefault(x => x.Id == loginId) ?? throw ShelfException.NotFound(MeErrorCodes.NoSuchSignIn);
        if (logins.Count == 1)
            throw ShelfException.Conflict(MeErrorCodes.LastSignIn);

        db.AccountLogins.Remove(login);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Settings ToModel(string? locale, AccountSettings settings) => new()
    {
        Locale = locale,
        ChordsScale = settings.ChordsScale,
        InterfaceScale = settings.InterfaceScale,
        LessMode = settings.LessMode,
        Accidentals = settings.Accidentals switch
        {
            AccidentalsMode.Flats => Accidentals.Flats,
            AccidentalsMode.Sharps => Accidentals.Sharps,
            _ => Accidentals.AsWritten,
        },
        ShowDegrees = settings.ShowDegrees,
        ChordSuperscripts = settings.ChordSuperscripts,
        Theme = settings.Theme switch
        {
            ThemeMode.Light => Theme.Light,
            ThemeMode.Dark => Theme.Dark,
            _ => Theme.System,
        },
    };

    private static bool IsScale(double value) => double.IsFinite(value) && value is >= MinScale and <= MaxScale;

    private static ShelfException Invalid(string setting)
        => ShelfException.BadRequest(MeErrorCodes.SettingInvalid, new Dictionary<string, string> { ["setting"] = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(setting) });
}
