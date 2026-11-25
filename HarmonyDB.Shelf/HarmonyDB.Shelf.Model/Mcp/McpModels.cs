namespace HarmonyDB.Shelf.Model.Mcp;

/// <summary>
/// The MCP page's head: the address of the shelf's MCP server, to paste into an assistant, and what the person may let
/// an assistant (or a key) do — the MCP permissions the admin gave them.
/// </summary>
public record McpOverview
{
    public required string ServerUrl { get; init; }

    /// <summary>
    /// The MCP permissions the person has now (<c>mcp.index</c>, <c>mcp.library.read</c> …): what may be ticked for an
    /// assistant or a key.
    /// </summary>
    public required IReadOnlyList<string> Grantable { get; init; }
}

/// <summary>
/// How an assistant came to the shelf.
/// </summary>
public enum McpClientKind
{
    /// <summary>It registered itself with the shelf (dynamic client registration).</summary>
    Registered,

    /// <summary>It is known by the address of its own description (a client id metadata document).</summary>
    MetadataDocument,
}

/// <summary>
/// An assistant that asks to connect, as it describes itself. Its name and address are its own words, not checked by
/// the shelf (anyone may call themselves after a well-known assistant): the hosts below are what tells them apart, and
/// are to be shown beside the name.
/// </summary>
public record McpClientInfo
{
    public required string ClientId { get; init; }

    public required McpClientKind Kind { get; init; }

    /// <summary>
    /// Null when the client gave no name: show <see cref="RedirectHost"/> instead.
    /// </summary>
    public string? Name { get; init; }

    public string? Uri { get; init; }

    /// <summary>
    /// For a client known by its own description document: the host of its address (its id), which only its owner can
    /// serve. Null for a registered client. Hosts are given in their ASCII form (<c>xn--…</c> for a name in other
    /// letters), so that a look-alike of a known host does not pass for it.
    /// </summary>
    public string? ClientIdHost { get; init; }

    /// <summary>
    /// The host the browser goes back to with the answer, in its ASCII form: shown to the person.
    /// </summary>
    public required string RedirectHost { get; init; }

    /// <summary>
    /// It goes back only to this computer (<c>localhost</c>): any program on the computer could claim to be it, so the
    /// person is warned.
    /// </summary>
    public required bool LocalOnly { get; init; }
}

/// <summary>
/// A pending request of an assistant to act for the person (the consent screen), by its id from the address
/// <c>/connect?request={id}</c>.
/// </summary>
public record AuthorizationRequestView
{
    public required McpClientInfo Client { get; init; }

    /// <summary>
    /// What the assistant asks for (MCP permissions); may be more than the person may give, or nothing (then it asks
    /// for whatever the person gives).
    /// </summary>
    public required IReadOnlyList<string> Requested { get; init; }

    /// <summary>
    /// What the person may give: the MCP permissions the admin gave them.
    /// </summary>
    public required IReadOnlyList<string> Grantable { get; init; }

    /// <summary>
    /// What the person already allowed this assistant, when it is connected; approving replaces it.
    /// </summary>
    public IReadOnlyList<string>? Allowed { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public record ApproveAuthorizationRequest
{
    /// <summary>
    /// What the assistant may do: MCP permissions the person has; at least one.
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }
}

/// <summary>
/// The answer to the consent: where to send the browser (back to the assistant, with a code or a refusal).
/// </summary>
public record AuthorizationDecision
{
    public required string RedirectUrl { get; init; }
}

/// <summary>
/// An assistant the person connected.
/// </summary>
public record ConnectedClient
{
    /// <summary>
    /// The connection's id (<c>/api/mcp/clients/{id}</c>).
    /// </summary>
    public required Guid Id { get; init; }

    public required McpClientKind Kind { get; init; }

    /// <summary>
    /// Null when the client gave no name: show <see cref="ClientIdHost"/> or <see cref="RedirectHosts"/> instead.
    /// </summary>
    public string? Name { get; init; }

    public string? Uri { get; init; }

    /// <summary>
    /// As in <see cref="McpClientInfo.ClientIdHost"/>.
    /// </summary>
    public string? ClientIdHost { get; init; }

    /// <summary>
    /// The hosts it may be sent back to, in their ASCII form.
    /// </summary>
    public required IReadOnlyList<string> RedirectHosts { get; init; }

    public required DateTimeOffset ConnectedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>
    /// What the person allowed it.
    /// </summary>
    public required IReadOnlyList<string> Allowed { get; init; }

    /// <summary>
    /// What it may do now: what the person allowed it and the admin gives the person.
    /// </summary>
    public required IReadOnlyList<string> InForce { get; init; }
}

public record UpdateMcpPermissionsRequest
{
    /// <summary>
    /// MCP permissions the person has; at least one.
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }
}

/// <summary>
/// A key for programs that cannot sign in. The key itself is shown once, when it is made.
/// </summary>
public record PersonalKeyItem
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// The key's first characters, to tell the keys apart.
    /// </summary>
    public required string Prefix { get; init; }

    public required IReadOnlyList<string> Allowed { get; init; }

    public required IReadOnlyList<string> InForce { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Null: it does not expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }
}

public record CreatePersonalKeyRequest
{
    public required string Name { get; init; }

    /// <summary>
    /// MCP permissions the person has; at least one.
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }

    /// <summary>
    /// 1 … 365; null: it does not expire.
    /// </summary>
    public int? ExpiresInDays { get; init; }
}

/// <summary>
/// A key just made: <see cref="Key"/> is shown this once and never again.
/// </summary>
public record CreatedPersonalKey
{
    public required string Key { get; init; }

    public required PersonalKeyItem Item { get; init; }
}

public static class McpErrorCodes
{
    /// <summary>404: no such connected assistant of the person's.</summary>
    public const string NoSuchClient = "no_such_mcp_client";

    /// <summary>404: no such key of the person's.</summary>
    public const string NoSuchKey = "no_such_personal_key";

    /// <summary>400: a permission that is not an MCP permission, or one the person does not have (details:
    /// <c>permission</c>).</summary>
    public const string NotGrantable = "permission_not_grantable";

    /// <summary>400: no permission chosen.</summary>
    public const string NothingChosen = "nothing_chosen";

    /// <summary>400: a key's name is needed, at most 100 characters (details: <c>max</c>).</summary>
    public const string KeyNameRequired = "personal_key_name_required";

    /// <summary>400: a key's expiry is 1 … 365 days.</summary>
    public const string KeyExpiryInvalid = "personal_key_expiry_invalid";

    /// <summary>
    /// 404 at <c>/api/oauth/requests/{id}</c>, and the error of <c>/connect?error=…</c>: the request is not known,
    /// expired, or its assistant is gone; the person starts connecting again from the assistant.
    /// </summary>
    public const string RequestInvalid = "authorization_request_invalid";

    /// <summary>409 at <c>/api/oauth/requests/{id}</c>: the request was already approved or refused; the person starts
    /// connecting again from the assistant if they want to change it.</summary>
    public const string RequestDecided = "authorization_request_decided";

    /// <summary>The error of <c>/connect?error=…</c>: the assistant is not known to the shelf, or its description could
    /// not be read or is not valid.</summary>
    public const string UnknownClient = "unknown_mcp_client";

    /// <summary>The error of <c>/connect?error=…</c>: the assistant asked to be sent back to an address it did not
    /// register.</summary>
    public const string RedirectMismatch = "redirect_uri_mismatch";
}
