using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.App.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace HarmonyDB.Shelf.Api.Areas.Music.Services;

/// <summary>
/// The language the music services' names (genres, moods, their pages' titles) are given in, as the app speaks:
/// the one the app names (its <c>locale</c>), else the person's own setting, else the browser's first language the app
/// has, else the app's default — one of <see cref="PublicConfigurationService.Locales"/>.
/// </summary>
public sealed class MusicLanguages(ShelfDbContext db)
{
    /// <param name="asked">The app's language, when it names it; one that is not the app's is refused (400).</param>
    /// <param name="acceptLanguage">The browser's languages (the request's <c>Accept-Language</c>), when there is a browser.</param>
    public async Task<string> OfAsync(ClaimsPrincipal user, string? asked, string? acceptLanguage, CancellationToken cancellationToken)
    {
        var locales = PublicConfigurationService.Locales;
        if (asked != null)
            return locales.Contains(asked) ? asked : throw ShelfException.BadRequest();

        if (user.GetAccountId() is { } accountId
            && await db.Accounts.Where(x => x.Id == accountId).Select(x => x.Locale).SingleOrDefaultAsync(cancellationToken) is { } own
            && locales.Contains(own))
            return own;

        if (acceptLanguage != null && StringWithQualityHeaderValue.TryParseList([acceptLanguage], out var languages))
        {
            var browser = languages
                .OrderByDescending(x => x.Quality ?? 1)
                .Select(x => x.Value.Value?.Split('-')[0].ToLowerInvariant())
                .FirstOrDefault(x => x != null && locales.Contains(x));
            if (browser != null)
                return browser;
        }

        return locales[0];
    }
}
