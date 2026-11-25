using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// The shelf's own analysis of one recording, served as a source (source contract, version 2). An analysis of a music
/// service's recording is open to all; one of a person's own file, to that person only.
/// </summary>
public class Analysis
{
    public Guid Id { get; set; }

    /// <summary>
    /// The id it is given out by as a source: opaque, stable.
    /// </summary>
    public required string SourceId { get; set; }

    public required string Title { get; set; }

    public List<string> Artists { get; set; } = [];

    /// <summary>
    /// The music service's recording it was made of, when it was made of one.
    /// </summary>
    public Guid? RecordingId { get; set; }

    public Recording? Recording { get; set; }

    /// <summary>
    /// Set when it was made of a person's own file: it is open to that person only.
    /// </summary>
    public Guid? OwnerId { get; set; }

    public Account? Owner { get; set; }

    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Who put it here: an internal client's name.
    /// </summary>
    public required string CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<AnalysisContent> Contents { get; set; } = [];

    public List<AnalysisFile> Files { get; set; } = [];
}

/// <summary>
/// One kind of an analysis's content, as the source contract's model of that kind, JSON.
/// </summary>
public class AnalysisContent
{
    public Guid AnalysisId { get; set; }

    public Analysis Analysis { get; set; } = null!;

    /// <summary>
    /// The source contract's content kind as its wire format writes it ("timeline", "lyrics" …).
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// The kind's model of the source contract, written with its one set of JSON options (<c>SourceV2Json.Options</c>).
    /// </summary>
    public required string Content { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A file of an analysis (the recording, a separate instrument, a MIDI file), kept in the music service's store.
/// </summary>
public class AnalysisFile
{
    public Guid AnalysisId { get; set; }

    public Analysis Analysis { get; set; } = null!;

    public required string Name { get; set; }

    /// <summary>
    /// Its key in the music service's store.
    /// </summary>
    public required string BlobKey { get; set; }

    public required string ContentType { get; set; }

    public long Size { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class AnalysisConfiguration : IEntityTypeConfiguration<Analysis>
{
    public void Configure(EntityTypeBuilder<Analysis> builder)
    {
        builder.ToTable("analyses");
        builder.HasIndex(x => x.SourceId).IsUnique();
        builder.HasIndex(x => x.RecordingId);
        builder.HasIndex(x => x.OwnerId);
        builder.HasOne(x => x.Recording).WithMany().HasForeignKey(x => x.RecordingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AnalysisContentConfiguration : IEntityTypeConfiguration<AnalysisContent>
{
    public void Configure(EntityTypeBuilder<AnalysisContent> builder)
    {
        builder.ToTable("analysis_contents");
        builder.HasKey(x => new { x.AnalysisId, x.Kind });
        builder.Property(x => x.Content).HasColumnType("jsonb");
        builder.HasOne(x => x.Analysis).WithMany(x => x.Contents).HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AnalysisFileConfiguration : IEntityTypeConfiguration<AnalysisFile>
{
    public void Configure(EntityTypeBuilder<AnalysisFile> builder)
    {
        builder.ToTable("analysis_files");
        builder.HasKey(x => new { x.AnalysisId, x.Name });
        builder.HasOne(x => x.Analysis).WithMany(x => x.Files).HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Cascade);
    }
}
