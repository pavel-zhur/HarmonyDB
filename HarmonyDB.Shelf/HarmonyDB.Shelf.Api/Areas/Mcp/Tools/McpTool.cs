using System.Text.Json.Nodes;
using HarmonyDB.Shelf.Api.Areas.Mcp.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using ModelContextProtocol.Server;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Tools;

/// <summary>
/// What a tool does to the world, for its annotations.
/// </summary>
public enum McpToolEffect
{
    /// <summary>Reads the shelf.</summary>
    Read,

    /// <summary>Reads a music service out there.</summary>
    ReadOutside,

    /// <summary>Changes the shelf; doing it again leaves the same state.</summary>
    Set,

    /// <summary>Adds to the shelf; doing it again adds again.</summary>
    Add,

    /// <summary>Removes from the shelf (undo may bring it back for a short while).</summary>
    Remove,
}

/// <summary>
/// One tool: its stable name, what it is for (written for an assistant that knows nothing of the shelf), the
/// permission it needs (the one place that decides is the authorization of the MCP server: a tool the caller may not
/// use is not listed to it and cannot be called), and its handler — a call of the same service the app's endpoint
/// calls. The handler's parameters are its arguments (with their descriptions), and the services and the caller it
/// needs.
/// </summary>
public sealed record McpTool(string Name, string Description, McpToolEffect Effect, Delegate Handler, params string[] Permissions)
{
    /// <summary>
    /// A tool that needs any one of its permissions (its <see cref="Permissions"/> are then alternatives).
    /// </summary>
    public bool AnyPermission { get; init; }

    public McpServerTool Create(IServiceProvider services) => McpServerTool.Create(Handler, new McpServerToolCreateOptions
    {
        Name = Name,
        Description = Description,
        Services = services,
        SerializerOptions = McpJson.Options,
        ReadOnly = Effect is McpToolEffect.Read or McpToolEffect.ReadOutside,
        Destructive = Effect == McpToolEffect.Remove,
        Idempotent = Effect is McpToolEffect.Read or McpToolEffect.ReadOutside or McpToolEffect.Set,
        OpenWorld = Effect == McpToolEffect.ReadOutside,
        Metadata = AnyPermission
            ? [new RequireAnyPermissionAttribute(Permissions)]
            : Permissions.Select(x => (object)new RequirePermissionAttribute(x)).ToList(),
        SchemaCreateOptions = new() { TransformSchemaNode = (_, node) => Lean(node) },
    });

    /// <summary>
    /// The listing is in every assistant's context: an optional argument is said by its absence from "required", so its
    /// schema carries no "null" type, null enum value or null default.
    /// </summary>
    private static JsonNode Lean(JsonNode node)
    {
        if (node is not JsonObject schema)
            return node;

        if (schema.TryGetPropertyValue("default", out var value) && value == null)
            schema.Remove("default");

        if (schema["type"] is JsonArray types && types.Count == 2 && types.Any(x => x?.GetValue<string>() == "null"))
            schema["type"] = types.First(x => x?.GetValue<string>() != "null")!.GetValue<string>();

        if (schema["enum"] is JsonArray values)
            schema["enum"] = new JsonArray(values.Where(x => x != null).Select(x => x!.DeepClone()).ToArray());

        return schema;
    }
}
