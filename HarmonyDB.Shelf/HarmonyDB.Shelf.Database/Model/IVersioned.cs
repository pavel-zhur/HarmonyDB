namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// A row whose changes are handed out by version (the sync of the browser's small changes, and everything a member
/// sees change in a playlist).
/// </summary>
public interface IVersioned
{
    /// <summary>
    /// The id of the database transaction that wrote the row last. Set by the database (a trigger, see
    /// <see cref="Migrations.VersionTriggerMigrationExtensions"/>) on every insert and update; never written by code.
    /// Rows changed since a reader's watermark (<see cref="ShelfDbContext.GetChangeWatermarkAsync"/>) have a
    /// <see cref="Version"/> at or above it.
    /// </summary>
    long Version { get; }
}

/// <summary>
/// A row that is not removed but marked removed, so that a removal reaches every reader as a change (a tombstone) and
/// can be undone. The model's query filters hide removed rows; <c>IgnoreQueryFilters()</c> reads them.
/// <para>
/// The rules, the same for every such table:
/// </para>
/// <list type="bullet">
/// <item>Uniqueness holds among the rows not removed (the unique indexes are partial, on <c>deleted_at IS NULL</c>).
/// A removed row is history: adding the same thing again makes a new row.</item>
/// <item>Undo brings back the removed row itself (by its id, <c>DeletedAt</c> cleared). When a row not removed has
/// taken its place meanwhile, there is nothing to undo.</item>
/// <item>Nothing that others see is hard-deleted while they may still sync: a playlist is removed by marking it (and
/// its members, songs and items stay as they are, hidden with it); a profile, as <see cref="Account.DeletedAt"/>
/// says. Hard deletes (and the cascades they bring) are the purge's alone, for rows removed longer than the
/// tombstones are kept.</item>
/// </list>
/// </summary>
public interface ITombstoned
{
    DateTimeOffset? DeletedAt { get; set; }
}
