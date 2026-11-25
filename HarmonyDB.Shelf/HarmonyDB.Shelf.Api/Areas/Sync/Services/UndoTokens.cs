using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Secrets;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Playlists;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HarmonyDB.Shelf.Api.Areas.Sync.Services;

/// <summary>
/// One step of an undo: a row (by its key) set back to the values it had, marked removed again (it was added), or
/// deleted (a row that is not kept as a tombstone); or an invitation's use given back (joining by it undone).
/// </summary>
public sealed record UndoStep
{
    public const string SetOp = "set";
    public const string RemoveOp = "remove";
    public const string DeleteOp = "delete";
    public const string ReturnInvitationUseOp = "return_invitation_use";

    [JsonPropertyName("o")] public required string Op { get; init; }

    [JsonPropertyName("e")] public string? Entity { get; init; }

    [JsonPropertyName("k")] public Dictionary<string, JsonElement>? Key { get; init; }

    [JsonPropertyName("v")] public Dictionary<string, JsonElement>? Values { get; init; }

    [JsonPropertyName("t")] public string? Token { get; init; }

    public static UndoStep ReturnInvitationUse(string token) => new() { Op = ReturnInvitationUseOp, Token = token };
}

/// <summary>
/// Undo of every change: the change's steps are taken from what the database context is about to save
/// (<see cref="Capture"/>), sealed into a token signed for the person, valid for <see cref="Lifetime"/>, and given back
/// with the change's answer; <c>POST /api/undo</c> with the token applies them. A removed row is brought back itself
/// (by its id); when a row not removed has taken its place meanwhile, there is nothing to undo
/// (409 <c>nothing_to_undo</c>), and nothing is changed.
/// </summary>
public sealed class UndoTokens(ShelfDbContext db, IOptionsMonitor<SecretsOptions> secrets, TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The rows an undo may touch, with how to find one by its key.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<ShelfDbContext, Dictionary<string, JsonElement>, Task<object?>>> Loaders =
        new Dictionary<string, Func<ShelfDbContext, Dictionary<string, JsonElement>, Task<object?>>>
        {
            [nameof(Playlist)] = async (db, k) => await db.Playlists.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(PlaylistMember)] = async (db, k) => await db.PlaylistMembers.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(PlaylistFollow)] = async (db, k) => await db.PlaylistFollows.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(PlaylistSong)] = async (db, k) => await db.PlaylistSongs.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(PlaylistSongItem)] = async (db, k) => await db.PlaylistSongItems.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(Mark)] = async (db, k) => await db.Marks.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(Transposition)] = async (db, k) => await db.Transpositions.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(Note)] = async (db, k) => await db.Notes.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == Id(k)),
            [nameof(PlaylistSongPreference)] = async (db, k) =>
            {
                Guid account = k["AccountId"].GetGuid(), song = k["SongId"].GetGuid();
                return await db.PlaylistSongPreferences.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.AccountId == account && x.SongId == song);
            },
            [nameof(PlaylistImportTombstone)] = async (db, k) =>
            {
                Guid playlist = k["PlaylistId"].GetGuid();
                var track = k["OriginTrackId"].GetString();
                return await db.PlaylistImportTombstones.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.PlaylistId == playlist && x.OriginTrackId == track);
            },
            [nameof(PlaylistInvitation)] = async (db, k) =>
            {
                var token = k["Token"].GetString();
                return await db.PlaylistInvitations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Token == token);
            },
        };

    /// <summary>
    /// The steps that undo what the context is about to save, for the rows an undo may touch: a changed row is set
    /// back, an added one removed (an added preference cleared, an added invitation revoked).
    /// </summary>
    public static List<UndoStep> Capture(ShelfDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var steps = new List<UndoStep>();
        foreach (var entry in db.ChangeTracker.Entries())
        {
            var name = entry.Metadata.ClrType.Name;
            if (!Loaders.ContainsKey(name))
                continue;

            switch (entry.State)
            {
                case EntityState.Modified:
                    var values = entry.Properties
                        .Where(x => x.IsModified && x.Metadata.Name != nameof(IVersioned.Version))
                        .ToDictionary(x => x.Metadata.Name, x => JsonSerializer.SerializeToElement(x.OriginalValue, x.Metadata.ClrType, Json));
                    if (values.Count > 0)
                        steps.Add(new() { Op = UndoStep.SetOp, Entity = name, Key = KeyOf(entry), Values = values });
                    break;
                case EntityState.Added when entry.Entity is ITombstoned or PlaylistInvitation:
                    steps.Add(new() { Op = UndoStep.RemoveOp, Entity = name, Key = KeyOf(entry) });
                    break;
                case EntityState.Added when entry.Entity is PlaylistSongPreference:
                    steps.Add(new()
                    {
                        Op = UndoStep.SetOp, Entity = name, Key = KeyOf(entry),
                        Values = new()
                        {
                            [nameof(PlaylistSongPreference.MainItemId)] = JsonSerializer.SerializeToElement<Guid?>(null),
                            [nameof(PlaylistSongPreference.RecordingItemId)] = JsonSerializer.SerializeToElement<Guid?>(null),
                        },
                    });
                    break;
                case EntityState.Added when entry.Entity is PlaylistImportTombstone:
                    steps.Add(new() { Op = UndoStep.DeleteOp, Entity = name, Key = KeyOf(entry) });
                    break;
            }
        }

        return steps;
    }

    /// <summary>
    /// The token of the steps for the person; null when there is nothing to undo.
    /// </summary>
    public string? Issue(Guid accountId, IReadOnlyList<UndoStep> steps)
    {
        if (steps.Count == 0)
            return null;

        var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload
        {
            AccountId = accountId,
            ExpiresAt = time.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds(),
            Steps = steps,
        }, Json);
        var body = WebEncoders.Base64UrlEncode(payload);
        return $"{body}.{Sign(body)}";
    }

    /// <summary>
    /// Captures what the context is about to save, saves it, and gives the token that undoes it.
    /// </summary>
    public async Task<string?> SaveAsync(Guid accountId, CancellationToken cancellationToken, params UndoStep[] more)
    {
        var steps = Capture(db);
        steps.AddRange(more);
        await db.SaveChangesAsync(cancellationToken);
        return Issue(accountId, steps);
    }

    /// <summary>
    /// Applies the token's steps, all or none.
    /// </summary>
    public async Task UndoAsync(Guid accountId, string token, CancellationToken cancellationToken)
    {
        var payload = Read(token);
        if (payload == null || payload.AccountId != accountId || DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt) < time.GetUtcNow())
            throw new ShelfException(StatusCodes.Status410Gone, PlaylistErrorCodes.UndoExpired);

        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // step by step, in their order: a step may need the one before it done (one owner at a time …)
            foreach (var step in payload.Steps)
            {
                await ApplyAsync(accountId, step, now, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            // a follow brought back counts only where the person is no member (they joined meanwhile): nothing to undo
            var follows = db.ChangeTracker.Entries<PlaylistFollow>().Select(x => x.Entity).Where(x => x.DeletedAt == null).ToList();
            foreach (var follow in follows)
            {
                if (await db.PlaylistMembers.AnyAsync(m => m.PlaylistId == follow.PlaylistId && m.AccountId == follow.AccountId, cancellationToken))
                    throw ShelfException.Conflict(PlaylistErrorCodes.NothingToUndo);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ShelfException.Conflict(PlaylistErrorCodes.NothingToUndo);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ShelfException.Conflict(PlaylistErrorCodes.NothingToUndo);
        }
    }

    private async Task ApplyAsync(Guid accountId, UndoStep step, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (step.Op == UndoStep.ReturnInvitationUseOp)
        {
            await db.PlaylistInvitations.IgnoreQueryFilters()
                .Where(x => x.Token == step.Token && x.Uses > 0)
                .ExecuteUpdateAsync(x => x.SetProperty(i => i.Uses, i => i.Uses - 1), cancellationToken);
            return;
        }

        if (step.Entity == null || step.Key == null || !Loaders.TryGetValue(step.Entity, out var loader))
            throw new ShelfException(StatusCodes.Status410Gone, PlaylistErrorCodes.UndoExpired);

        var row = await loader(db, step.Key);
        if (row == null)
            return;

        var entry = db.Entry(row);
        switch (step.Op)
        {
            case UndoStep.SetOp:
                foreach (var (property, value) in step.Values ?? [])
                {
                    var target = entry.Property(property);
                    target.CurrentValue = value.Deserialize(target.Metadata.ClrType, Json);
                }

                break;
            case UndoStep.RemoveOp when row is ITombstoned tombstoned:
                tombstoned.DeletedAt ??= now;
                break;
            case UndoStep.RemoveOp when row is PlaylistInvitation invitation:
                invitation.RevokedAt ??= now;
                break;
            case UndoStep.DeleteOp:
                entry.State = EntityState.Deleted;
                break;
        }
    }

    private Payload? Read(string token)
    {
        var dot = token.IndexOf('.');
        if (dot <= 0)
            return null;

        var body = token[..dot];
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(body)), Encoding.ASCII.GetBytes(token[(dot + 1)..])))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Payload>(WebEncoders.Base64UrlDecode(body), Json);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return null;
        }
    }

    private string Sign(string body)
    {
        var options = secrets.CurrentValue;
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, options.GetKey(options.KeyVersion), 32, info: "HarmonyDB.Shelf undo"u8.ToArray());
        return WebEncoders.Base64UrlEncode(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(body)));
    }

    private static Dictionary<string, JsonElement> KeyOf(EntityEntry entry)
        => entry.Metadata.FindPrimaryKey()!.Properties
            .ToDictionary(x => x.Name, x => JsonSerializer.SerializeToElement(entry.Property(x.Name).CurrentValue, x.ClrType, Json));

    private static Guid Id(Dictionary<string, JsonElement> key) => key["Id"].GetGuid();

    private sealed record Payload
    {
        [JsonPropertyName("a")] public Guid AccountId { get; init; }

        [JsonPropertyName("x")] public long ExpiresAt { get; init; }

        [JsonPropertyName("s")] public IReadOnlyList<UndoStep> Steps { get; init; } = [];
    }
}
