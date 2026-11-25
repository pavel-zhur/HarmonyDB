using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using HarmonyDB.Shelf.Api.Foundation;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// The JSON of the tools' arguments and answers: the browser API's (camel case, enums as camel-case strings), with
/// the empty values left out.
/// </summary>
public static class McpJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            // read by an assistant, never put into HTML: words and chords ('<Am>') as they are, not as \u escapes
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        FoundationExtensions.ConfigureJson(options);
        return options;
    }
}

/// <summary>
/// Met when any one of the permissions is in force for the caller: for a tool that serves the work of several
/// (the person's jobs: books, imports).
/// </summary>
public sealed record AnyPermissionRequirement(IReadOnlyList<string> Permissions) : IAuthorizationRequirement;

internal sealed class AnyPermissionAuthorizationHandler(PermissionCatalog catalog) : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AnyPermissionRequirement requirement)
    {
        if (requirement.Permissions.Any(x => catalog.Has(context.User, x)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAnyPermissionAttribute(params string[] permissions) : Attribute, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new AnyPermissionRequirement(permissions);
    }
}

/// <summary>
/// The person a tool acts for (the MCP server is reached with a token only, so there is always one).
/// </summary>
internal static class McpCaller
{
    public static Guid Me(this ClaimsPrincipal user) => user.GetAccountId()!.Value;
}
