using HarmonyDB.Shelf.Model.Admin;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Admin.Web.ShelfAdmin;

namespace OneShelf.Admin.Web.Controllers;

/// <summary>
/// The people of the new shelf and their permissions, through the shelf's internal admin API.
/// </summary>
[ShelfUnavailable]
public class ShelfPeopleController(ShelfAdminClient shelf) : Controller
{
    private const string MessageKey = "ShelfMessage";
    private const string ErrorKey = "ShelfError";

    public const int PageSize = 50;

    /// <summary>
    /// The value of <c>has</c> that keeps the people who have some permission.
    /// </summary>
    public const string AnyPermission = "*";

    /// <param name="q">The search.</param>
    /// <param name="has">Only who has this permission; <see cref="AnyPermission"/>: who has some.</param>
    public async Task<IActionResult> Index(string? q, string? has, int skip = 0, CancellationToken cancellationToken = default)
    {
        if (!shelf.IsConfigured)
            return View("NotConfigured");

        skip = Math.Max(skip, 0);
        var permission = has is null or AnyPermission ? null : has;
        var page = await shelf.ListPeopleAsync(q, permission, has == AnyPermission, skip, PageSize, cancellationToken);

        // a page past the end (a typed address): the last one there is
        if (skip > 0 && skip >= page.Total)
            return RedirectToAction(nameof(Index), new { q, has, skip = page.Total == 0 ? 0 : (page.Total - 1) / PageSize * PageSize });

        var catalog = await shelf.GetPermissionCatalogAsync(cancellationToken);
        return View(new ShelfPeopleModel(page, catalog, q, has, skip));
    }

    public async Task<IActionResult> Person(Guid id, CancellationToken cancellationToken)
    {
        if (!shelf.IsConfigured)
            return View("NotConfigured");

        AdminPerson person;
        try
        {
            person = await shelf.GetPersonAsync(id, cancellationToken);
        }
        catch (ShelfAdminException e) when (e.Code == AdminErrorCodes.NoSuchPerson)
        {
            return NoSuchPerson();
        }

        var catalog = await shelf.GetPermissionCatalogAsync(cancellationToken);
        var changes = await shelf.GetPermissionChangesAsync(id, 20, cancellationToken);
        return View(new ShelfPersonModel(person, catalog, changes));
    }

    /// <param name="p">The ticked permissions.</param>
    /// <param name="expected">The permissions the page was showing as the person's.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Person(Guid id, string[]? p, string[]? expected, CancellationToken cancellationToken)
    {
        if (!shelf.IsConfigured)
            return View("NotConfigured");

        try
        {
            var change = await shelf.SetPermissionsAsync(id, new() { Permissions = p ?? [], Expected = expected ?? [] }, cancellationToken);
            TempData[MessageKey] = change.Added.Count == 0 && change.Removed.Count == 0
                ? "Nothing changed."
                : "Saved.";
        }
        catch (ShelfAdminException e) when (e.Code == AdminErrorCodes.NoSuchPerson)
        {
            return NoSuchPerson();
        }
        catch (ShelfAdminException e) when (e.Code == AdminErrorCodes.PermissionsChangedMeanwhile)
        {
            TempData[ErrorKey] = "Not saved: the permissions were changed elsewhere meanwhile. Here they are as they stand now; tick again.";
        }
        catch (ShelfAdminException e) when (e.Code == AdminErrorCodes.UnknownPermission)
        {
            TempData[ErrorKey] = $"Not saved: the shelf does not know the permission \"{e.Details?.GetValueOrDefault("permission")}\" (it is no longer in its configuration).";
        }
        catch (ShelfAdminException e) when (e.Code == AdminErrorCodes.PersonDeleted)
        {
            TempData[ErrorKey] = "Not saved: this profile is deleted.";
        }

        return RedirectToAction(nameof(Person), new { id });
    }

    public async Task<IActionResult> Changes(CancellationToken cancellationToken)
    {
        if (!shelf.IsConfigured)
            return View("NotConfigured");

        var changes = await shelf.GetPermissionChangesAsync(null, 200, cancellationToken);
        var catalog = await shelf.GetPermissionCatalogAsync(cancellationToken);
        return View(new ShelfChangesModel(changes, catalog));
    }

    private ViewResult NoSuchPerson()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("Message", "There is no such person in the shelf (the account may have been removed).");
    }
}
