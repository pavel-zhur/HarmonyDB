using Microsoft.AspNetCore.Authorization;

namespace HarmonyDB.Common.ServiceAuth;

/// <summary>
/// The endpoint is for internal clients only, authenticated by their key, that have this scope.
/// On a minimal API endpoint the same is <see cref="EndpointConventionBuilderExtensions.RequireInternalScope{TBuilder}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireInternalScopeAttribute(string scope) : Attribute, IAuthorizeData, IAuthorizationRequirementData
{
    public string Scope { get; } = scope;

    string? IAuthorizeData.Policy
    {
        get => null;
        set => throw new NotSupportedException();
    }

    string? IAuthorizeData.Roles
    {
        get => null;
        set => throw new NotSupportedException();
    }

    string? IAuthorizeData.AuthenticationSchemes
    {
        get => InternalKeyDefaults.Scheme;
        set => throw new NotSupportedException();
    }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new InternalScopeRequirement(Scope);
    }
}
