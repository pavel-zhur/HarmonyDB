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
/// A <c>chords</c> row keeps what the source gave of the item on its last reading by id — the title, artists, kinds and
/// the track it was made from: by an id a source gives an item to everyone alike. It keeps no source's label: that is
/// each person's own (their device asks it of the source).
/// </para>
/// </summary>
public sealed class ItemRows(ShelfDbContext db, TimeProvider time)
{
    /// <summary>
    /// The local row of the source's item, made when there is none.
    /// </summary>
    /// <param name="sourceId">The source's opaque id (source contract, version 2).</param>
    /// <param name="header">The item's header as a caller just got it; null to leave the row's as it is. An empty title
    /// never replaces a title.</param>
    public async Task<ChordsItem> GetOrCreateChordsAsync(string sourceId, Header? header, CancellationToken cancellationToken)
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

        if (header != null)
        {
            if (!string.IsNullOrWhiteSpace(header.Title))
            {
                row.Title = header.Title;
                row.Artists = header.Artists.ToList();
            }

            row.Kinds = header.Kinds.Select(KindName).ToList();
            // what it was made from is told to everyone (the music pages): kept only of an item that may be shown publicly
            row.MadeFromRecordingId = header.IsPublic && header.MadeFrom is { } track && MusicServiceNames.StoredOf(track.Service) is { } service
                ? (await GetOrCreateRecordingAsync(service, track.Id, null, cancellationToken)).Id
                : null;
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
