using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Analyses.Database.Model;

/// <summary>
/// An analysis of one recording, with all it has: open to all.
/// </summary>
public class Analysis
{
    public Guid Id { get; set; }

    /// <summary>
    /// The id it is given out by as a source's item: opaque, stable, made of who put it and its key.
    /// </summary>
    public required string SourceId { get; set; }

    /// <summary>
    /// Who put it here: an internal client's name.
    /// </summary>
    public required string CreatedBy { get; set; }

    public required string Title { get; set; }

    public List<string> Artists { get; set; } = [];

    /// <summary>
    /// Who ordered it, as an opaque string, when it was told. It decides nothing and is never given out.
    /// </summary>
    public string? OrderedBy { get; set; }

    /// <summary>
    /// The music service of the track it was made from, when it was made from one.
    /// </summary>
    public string? MadeFromService { get; set; }

    /// <summary>
    /// The track's id on that service.
    /// </summary>
    public string? MadeFromId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<AnalysisContent> Contents { get; set; } = [];

    public List<AnalysisFile> Files { get; set; } = [];
}

/// <summary>
/// One kind of an analysis's content: the source contract's model of that kind, JSON, kept and given as it was put.
/// </summary>
public class AnalysisContent
{
    public Guid AnalysisId { get; set; }

    public Analysis Analysis { get; set; } = null!;

    /// <summary>
    /// The content kind as the source contract's wire format writes it ("timeline", "lyrics" …).
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// The kind's model, written with the source contract's one set of JSON options.
    /// </summary>
    public required string Content { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A file of an analysis that its content names (the recording, a separate instrument, a MIDI file): written down when
/// the content is put, with the kind that names it; kept in the analyses' folder once it is uploaded.
/// </summary>
public class AnalysisFile
{
    public Guid AnalysisId { get; set; }

    public Analysis Analysis { get; set; } = null!;

    /// <summary>
    /// Its name, as the content names it.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The content kind that names it, as the source contract's wire format writes it ("recording", "notes").
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// Its content type, as the content names it.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// The name of its file in the analysis's folder, a new one for each upload, so an upload never touches the file
    /// being read; null while it is not uploaded.
    /// </summary>
    public string? StoredName { get; set; }

    public long? Size { get; set; }

    /// <summary>
    /// The SHA-256 of its bytes, lower-case hex.
    /// </summary>
    public string? Sha256 { get; set; }

    public DateTimeOffset? StoredAt { get; set; }
}

internal sealed class AnalysisConfiguration : IEntityTypeConfiguration<Analysis>
{
    public void Configure(EntityTypeBuilder<Analysis> builder)
    {
        builder.ToTable("analyses");
        builder.HasIndex(x => x.SourceId).IsUnique();
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
