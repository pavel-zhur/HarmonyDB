using Microsoft.AspNetCore.Builder;

namespace HarmonyDB.Common.ServiceAuth;

public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// The endpoints are for internal clients only, authenticated by their key, that have this scope.
    /// </summary>
    public static TBuilder RequireInternalScope<TBuilder>(this TBuilder builder, string scope)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequireInternalScopeAttribute(scope));
}
