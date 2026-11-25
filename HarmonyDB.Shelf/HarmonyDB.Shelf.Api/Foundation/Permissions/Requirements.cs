using Microsoft.AspNetCore.Authorization;

namespace HarmonyDB.Shelf.Api.Foundation.Permissions;

/// <summary>
/// Met when the permission is in force for the caller (a guest's defaults count).
/// </summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Met when the caller is a signed-in person in their own session; a client acting for the person (an MCP client, a
/// personal key) does not meet it: delegated calls go by permissions only.
/// </summary>
public sealed record SignedInRequirement : IAuthorizationRequirement;

internal sealed class PermissionAuthorizationHandler(PermissionCatalog catalog) : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements.ToList())
        {
            switch (requirement)
            {
                case PermissionRequirement permission when catalog.Has(context.User, permission.Permission):
                case SignedInRequirement when context.User.GetAccountId() != null && !context.User.IsDelegated():
                    context.Succeed(requirement);
                    break;
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// The endpoint needs the permission. A guest without it is answered 401 <c>sign_in_required</c>, a person without it
/// 403 <c>permission_denied</c>. On a minimal API endpoint, <c>.RequirePermission(…)</c> is the same.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute, IAuthorizationRequirementData
{
    public string Permission { get; } = permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Permission);
    }
}

/// <summary>
/// The endpoint is for signed-in people in their own session (every action of the app that writes): a guest is
/// answered 401 <c>sign_in_required</c>, the frontend offers sign-in and repeats the action; a client acting for the
/// person is answered 403 <c>forbidden</c>. On a minimal API endpoint, <c>.RequireSignIn()</c> is the same.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSignInAttribute : Attribute, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new SignedInRequirement();
    }
}

public static class PermissionEndpointExtensions
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequirePermissionAttribute(permission));

    public static TBuilder RequireSignIn<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequireSignInAttribute());
}
