using System.Security.Claims;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Areas.Admin.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Model.Admin;

namespace HarmonyDB.Shelf.Api.Areas.Admin;

/// <summary>
/// For the admin site, an internal client (scope <see cref="Scope"/>): the people of the shelf, the permissions there
/// are and the defaults, a person's permissions read and set, the latest changes of permissions, and the links imports
/// did not find.
/// </summary>
public sealed class AdminArea : IShelfArea
{
    public const string Scope = "shelf.admin";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PeopleAdmin>();
        services.AddScoped<UnreadLinksAdmin>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/admin").RequireInternalScope(Scope).WithTags("Admin");

        // search: a part of a name, user name or e-mail at a provider; a provider's id of the person; an account's id.
        // permission: only who has it; anyPermission: only who has some permission. All combine.
        group.MapGet("/people", (string? search, string? permission, bool? anyPermission, int? skip, int? take, PeopleAdmin people, CancellationToken cancellationToken)
            => people.ListAsync(search, permission, anyPermission ?? false, skip ?? 0, take ?? 50, cancellationToken)).WithName("AdminListPeople");

        group.MapGet("/people/{id:guid}", (Guid id, PeopleAdmin people, CancellationToken cancellationToken)
            => people.GetAsync(id, cancellationToken)).WithName("AdminGetPerson");

        group.MapPut("/people/{id:guid}/permissions", (Guid id, AdminSetPermissionsRequest request, ClaimsPrincipal client, PeopleAdmin people, CancellationToken cancellationToken)
            => people.SetPermissionsAsync(id, request, client.FindFirst(InternalKeyDefaults.ClientClaimType)!.Value, cancellationToken)).WithName("AdminSetPermissions");

        group.MapGet("/permissions", (PeopleAdmin people) => people.GetCatalog()).WithName("AdminGetPermissionCatalog");

        group.MapGet("/unread-links", (int? take, UnreadLinksAdmin links, CancellationToken cancellationToken)
            => links.ListAsync(take ?? 200, cancellationToken)).WithName("AdminListUnreadLinks");

        group.MapGet("/permission-changes", (Guid? accountId, int? take, PeopleAdmin people, CancellationToken cancellationToken)
            => people.GetChangesAsync(accountId, take ?? 100, cancellationToken)).WithName("AdminGetPermissionChanges");
    }
}
