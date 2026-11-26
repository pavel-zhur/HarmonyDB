using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Admin;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Admin.Services;

/// <summary>
/// The links imports of chords by links did not find, the latest met first.
/// </summary>
public sealed class UnreadLinksAdmin(ShelfDbContext db)
{
    public const int MaxLinks = 500;

    public async Task<IReadOnlyList<AdminUnreadLink>> ListAsync(int take, CancellationToken cancellationToken)
    {
        var links = await db.UnreadLinks.AsNoTracking()
            .GroupBy(x => x.Link)
            .Select(x => new { Link = x.Key, Times = x.Count(), FirstSeenAt = x.Min(l => l.SeenAt), LastSeenAt = x.Max(l => l.SeenAt) })
            .OrderByDescending(x => x.LastSeenAt).ThenBy(x => x.Link)
            .Take(Math.Clamp(take, 1, MaxLinks))
            .ToListAsync(cancellationToken);

        var shown = links.Select(x => x.Link).ToList();
        var people = (await db.UnreadLinks.AsNoTracking()
                .Where(x => shown.Contains(x.Link) && x.Account != null && x.Account.DeletedAt == null)
                .Select(x => new { x.Link, x.Account!.Id, x.Account.DisplayName })
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.Link, x => new AdminPersonRef { Id = x.Id, DisplayName = x.DisplayName });

        return links.Select(x => new AdminUnreadLink
        {
            Link = x.Link,
            Times = x.Times,
            FirstSeenAt = x.FirstSeenAt,
            LastSeenAt = x.LastSeenAt,
            People = people[x.Link].OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList(),
        }).ToList();
    }
}
