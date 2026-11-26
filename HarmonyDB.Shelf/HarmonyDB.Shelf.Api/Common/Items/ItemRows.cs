using System.Text.Json;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Common.Items;

/// <summary>
/// The local rows of things referenced here: a <c>chords</c> row for a source's item, a <c>recordings</c> row for a
/// music service's track. Got or made at once, safe when two requests make the same row together. Shared by the areas
/// that reference chords or recordings.
/// <para>
/// A <c>chords</c> row is seen by everyone, so it keeps <b>only what a caller with no keys and no account gets</b>: the
/// header (title, artists, kinds, the source's label) of an anonymous caller, and nothing a person with keys or an
/// account was given. An item not open to an anonymous caller keeps no title at all. What a person sees of an item —
/// its title included — comes from the header that person has just been given by the source, never from the row.
/// </para>
/// </summary>
public sealed class ItemRows(ShelfDbContext db, TimeProvider time)
{
    private static readonly TimeSpan HeaderRefresh = TimeSpan.FromHours(1);

    /// <summary>
    /// The local row of the source's item, made when there is none.
    /// </summary>
    /// <param name="sourceId">The source's opaque id (source contract, version 2).</param>
    /// <param name="header">The item's header as a caller just got it; null to leave the row's as it is.</param>
    /// <param name="anonymous">Whether that caller had no keys and no account. Only then is the header kept in the row;
    /// any other caller's header is not written. An empty title never replaces a title.</param>
    public async Task<ChordsItem> GetOrCreateChordsAsync(string sourceId, Header? header, bool anonymous, CancellationToken cancellationToken)
    {
        var row = await db.Chords.SingleOrDefaultAsync(x => x.SourceId == sourceId, cancellationToken);
        if (row == null)
        {
            var now = time.GetUtcNow();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO chords (id, source_id, artists, kinds, created_at) VALUES ({Guid.NewGuid()}, {sourceId}, {Array.Empty<string>()}, {Array.Empty<string>()}, {now}) ON CONFLICT (source_id) DO NOTHING",
                cancellationToken);
            row = await db.Chords.SingleAsync(x => x.SourceId == sourceId, cancellationToken);
        }

        // an anonymous caller's header, refreshed now and then, not at every reading
        if (anonymous && header != null && (row.HeaderUpdatedAt == null || row.HeaderUpdatedAt < time.GetUtcNow() - HeaderRefresh))
        {
            if (!string.IsNullOrWhiteSpace(header.Title))
            {
                row.Title = header.Title;
                row.Artists = header.Artists.ToList();
            }

            row.Kinds = header.Kinds.Select(KindName).ToList();
            row.SourceKey = header.Source.Key;
            row.SourceTitle = header.Source.Title;
            row.SourceUri = header.Address?.ToString();
            row.SourceIcon = header.Source.Icon;
            row.MadeFromRecordingId = header.MadeFrom is { } track && MusicServiceNames.StoredOf(track.Service) is { } service
                ? (await GetOrCreateRecordingAsync(service, track.Id, null, cancellationToken)).Id
                : null;
            row.HeaderUpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }

        return row;
    }


    /// <summary>
    /// The local row of the music service's track, made when there is none; its cached description refreshed when one
    /// is given.
    /// </summary>
    public async Task<Recording> GetOrCreateRecordingAsync(MusicService service, string serviceId, RecordingDescription? description, CancellationToken cancellationToken)
    {
        var row = await db.Recordings.SingleOrDefaultAsync(x => x.Service == service && x.ServiceId == serviceId, cancellationToken);
        var now = time.GetUtcNow();
        if (row == null)
        {
            var serviceName = MusicServiceNames.NameOf(service);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO recordings (id, service, service_id, artists, updated_at) VALUES ({Guid.NewGuid()}, {serviceName}, {serviceId}, {Array.Empty<string>()}, {now}) ON CONFLICT (service, service_id) DO NOTHING",
                cancellationToken);
            row = await db.Recordings.SingleAsync(x => x.Service == service && x.ServiceId == serviceId, cancellationToken);
        }

        if (description != null)
        {
            row.Title = description.Title;
            row.Artists = description.Artists.ToList();
            row.DurationSeconds = description.DurationSeconds;
            row.CoverUrl = description.CoverUrl;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return row;
    }

    /// <summary>
    /// A content kind as the source contract's wire format writes it ("sheet", "timeline" …).
    /// </summary>
    public static string KindName(ContentKind kind) => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());
}

/// <summary>
/// What is kept of a recording to show it without asking the music service.
/// </summary>
public sealed record RecordingDescription(string? Title, IReadOnlyList<string> Artists, double? DurationSeconds, string? CoverUrl);

public static class ItemRowsExtensions
{
    /// <summary>
    /// Adds <see cref="ItemRows"/>; safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddItemRows(this IServiceCollection services)
    {
        if (services.All(x => x.ServiceType != typeof(ItemRows)))
            services.AddScoped<ItemRows>();

        return services;
    }
}
