using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// One item of chords from a source, ever referenced here: what marks, keys, notes and playlists attach to. Its header
/// is a cache, as a caller with no keys last saw it; what a person sees is asked of the source for their keys.
/// </summary>
public class ChordsItem
{
    public Guid Id { get; set; }

    /// <summary>
    /// The source's opaque id of the item (source contract, version 2).
    /// </summary>
    public required string SourceId { get; set; }

    public string? Title { get; set; }

    public List<string> Artists { get; set; } = [];

    /// <summary>
    /// The source's label (key, title, address, icon name), as last seen; null when a caller with no keys may not open
    /// the item.
    /// </summary>
    public string? SourceKey { get; set; }

    public string? SourceTitle { get; set; }

    public string? SourceUri { get; set; }

    public string? SourceIcon { get; set; }

    /// <summary>
    /// The content kinds the item has, as the source contract's wire format writes them ("sheet", "timeline" …).
    /// </summary>
    public List<string> Kinds { get; set; } = [];

    public DateTimeOffset? HeaderUpdatedAt { get; set; }

    /// <summary>
    /// When recordings of the music services were last looked for for these chords (see <see cref="ChordsRecording"/>).
    /// </summary>
    public DateTimeOffset? RecordingsSearchedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A recording of a music service, ever referenced here, with its cached description.
/// </summary>
public class Recording
{
    public Guid Id { get; set; }

    public MusicService Service { get; set; }

    /// <summary>
    /// The track's id on the service.
    /// </summary>
    public required string ServiceId { get; set; }

    public string? Title { get; set; }

    public List<string> Artists { get; set; } = [];

    public double? DurationSeconds { get; set; }

    public string? CoverUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// The recordings found automatically for chords, on both services, best first: shared by everyone. A playlist keeps
/// its own choice in its songs' items.
/// </summary>
public class ChordsRecording
{
    public Guid ChordsId { get; set; }

    public ChordsItem Chords { get; set; } = null!;

    public Guid RecordingId { get; set; }

    public Recording Recording { get; set; } = null!;

    /// <summary>
    /// The order among the chords' recordings, from 0.
    /// </summary>
    public int Rank { get; set; }

    public DateTimeOffset FoundAt { get; set; }
}

internal sealed class ChordsItemConfiguration : IEntityTypeConfiguration<ChordsItem>
{
    public void Configure(EntityTypeBuilder<ChordsItem> builder)
    {
        builder.ToTable("chords");
        builder.HasIndex(x => x.SourceId).IsUnique();
    }
}

internal sealed class RecordingConfiguration : IEntityTypeConfiguration<Recording>
{
    public void Configure(EntityTypeBuilder<Recording> builder)
    {
        builder.ToTable("recordings");
        builder.HasIndex(x => new { x.Service, x.ServiceId }).IsUnique();
    }
}

internal sealed class ChordsRecordingConfiguration : IEntityTypeConfiguration<ChordsRecording>
{
    public void Configure(EntityTypeBuilder<ChordsRecording> builder)
    {
        builder.ToTable("chords_recordings");
        builder.HasKey(x => new { x.ChordsId, x.RecordingId });
        builder.HasOne(x => x.Chords).WithMany().HasForeignKey(x => x.ChordsId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Recording).WithMany().HasForeignKey(x => x.RecordingId).OnDelete(DeleteBehavior.Cascade);
    }
}
