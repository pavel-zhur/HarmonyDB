using System.Security.Claims;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Source.Api.Model.V2;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// What is too big for a tool's answer, given to an assistant as a short-lived link (<c>/mcp/files/{signed}</c>):
/// the file of a job of the person's (a PDF book …), or one kind of content of an item of chords (its full data, as
/// JSON). Whoever holds a link may download it until it expires, as with the media addresses. The content is read
/// when the link is opened, for the client that was given it, by what that client may do then: a client
/// disconnected (or a key revoked, a permission taken away) opens nothing.
/// </summary>
public sealed class McpFiles(SignedPayloads signed, McpAddresses addresses, TimeProvider time)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    private const string Purpose = "mcp file";

    /// <param name="JobId">A job's file, of the account's.</param>
    /// <param name="Client">A content: for this client (<see cref="McpPrincipals.ClientClaimType"/>).</param>
    private sealed record Target(Guid AccountId, Guid? JobId, string? Client, string? ChordsId, ContentKind? Kind);

    public (string Url, DateTimeOffset ExpiresAt) JobFile(Guid accountId, Guid jobId)
        => Link(new(accountId, jobId, null, null, null));

    public (string Url, DateTimeOffset ExpiresAt) Content(ClaimsPrincipal user, string chordsId, ContentKind kind)
        => Link(new(user.GetAccountId()!.Value, null, user.FindFirst(McpPrincipals.ClientClaimType)!.Value, chordsId, kind));

    private (string Url, DateTimeOffset ExpiresAt) Link(Target target)
    {
        var expiresAt = time.GetUtcNow() + LinkLifetime;
        return ($"{addresses.Url(McpAddresses.FilesPath)}/{signed.Sign(Purpose, target, expiresAt)}", expiresAt);
    }

    public async Task<IResult> ServeAsync(string token, IServiceProvider services, CancellationToken cancellationToken)
    {
        var target = signed.Verify<Target>(Purpose, token)?.Payload ?? throw new ShelfException(StatusCodes.Status410Gone, MediaErrorCodes.Expired);

        if (target.JobId is { } jobId)
        {
            var file = await services.GetRequiredService<JobQueue>().GetFileAsync(target.AccountId, jobId, cancellationToken)
                       ?? throw ShelfException.NotFound(JobsErrorCodes.NoFile);
            return Results.File(file.Data, file.ContentType, file.FileName);
        }

        var user = await services.GetRequiredService<McpPrincipals>().ByClientAsync(target.Client!, cancellationToken);
        if (user == null || user.GetAccountId() != target.AccountId || !services.GetRequiredService<PermissionCatalog>().Has(user, BuiltInPermissions.McpIndex))
            throw ShelfException.Forbidden(ErrorCodes.PermissionDenied);

        var content = await services.GetRequiredService<ChordsReader>().GetOpenContentAsync(user, target.ChordsId!, [target.Kind!.Value], null, cancellationToken)
                      ?? throw ShelfException.NotFound();
        var json = JsonSerializer.SerializeToElement(content, SourceV2Json.Options);
        return json.TryGetProperty(JsonNamingPolicy.CamelCase.ConvertName(target.Kind.Value.ToString()), out var kind) && kind.ValueKind != JsonValueKind.Null
            ? Results.Json(kind)
            : throw ShelfException.NotFound();
    }
}
