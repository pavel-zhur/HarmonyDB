using Microsoft.AspNetCore.Authorization;

namespace HarmonyDB.Common.ServiceAuth;

/// <summary>
/// Met when the caller is an internal client, authenticated by its key, that has the scope.
/// </summary>
public sealed record InternalScopeRequirement(string Scope) : IAuthorizationRequirement;

internal sealed class InternalScopeRequirementHandler : AuthorizationHandler<InternalScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, InternalScopeRequirement requirement)
    {
        var identity = context.User.Identities.FirstOrDefault(x => x is { IsAuthenticated: true, AuthenticationType: InternalKeyDefaults.Scheme });
        if (identity != null && identity.HasClaim(InternalKeyDefaults.ScopeClaimType, requirement.Scope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
