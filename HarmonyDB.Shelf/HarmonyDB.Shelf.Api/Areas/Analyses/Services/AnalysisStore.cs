using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyDB.Shelf.Api.Areas.Analyses.Services.Sheets;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recording = HarmonyDB.Source.Api.Model.V2.Contents.Recording;

namespace HarmonyDB.Shelf.Api.Areas.Analyses.Services;

/// <summary>
/// Puts analyses in: their content (in the source contract's kinds), and their files, streamed on into the music
/// service's store.
/// </summary>
public sealed partial class AnalysisStore(ShelfDbContext db, AudioBlobsClient blobs, IOptionsMonitor<AnalysesOptions> options, TimeProvider time)
{
    /// <summary>
    /// The prefix of the analyses' ids as a source's items.
    /// </summary>
    public const string IdPrefix = "analysis-";

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,99}$")]
    private static partial Regex FileName();

    /// <summary>
    /// The id of a client's analysis: made of the client's name and its key, so the same analysis put again is found.
    /// </summary>
    public static string IdOf(string client, string key)
        => IdPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{client}\n{key}")))[..32].ToLowerInvariant();

    /// <summary>
    /// The music service's key of a file of an analysis.
    /// </summary>
    public static string BlobKeyOf(Guid analysisId, string name) => $"analyses/{analysisId:N}/{name}";

    /// <summary>
    /// The content kinds as the source contract's wire format writes them ("timeline", "lyrics" …).
    /// </summary>
    public static string KindName(ContentKind kind) => JsonSerializer.Serialize(kind, SourceV2Json.Options).Trim('"');

    public async Task<PutAnalysisResponse> PutAsync(string client, PutAnalysisRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || request.Key.Length > 200 || string.IsNullOrWhiteSpace(request.Title) || request.Artists.Any(string.IsNullOrWhiteSpace))
            throw ShelfException.BadRequest();

        var content = request.Content;
        if (content.Sheet != null)
            throw ShelfException.BadRequest(AnalysesErrorCodes.SheetGiven);
        if (content.Timeline == null)
            throw ShelfException.BadRequest(AnalysesErrorCodes.TimelineMissing);

        // The sheet is made on every reading; made once here, it shows that every chord is readable.
        try
        {
            SheetBuilder.Build(content.Timeline, content.Lyrics);
        }
        catch (UnreadableChordException exception)
        {
            throw ShelfException.BadRequest(AnalysesErrorCodes.UnreadableChord, new Dictionary<string, string> { ["chord"] = exception.Name });
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            // a key, a meter, a tempo or a section that is not one
            throw ShelfException.BadRequest(AnalysesErrorCodes.BadContent);
        }

        if (request.OwnerId.HasValue && !await db.Accounts.AnyAsync(x => x.Id == request.OwnerId && x.DeletedAt == null, cancellationToken))
            throw ShelfException.BadRequest(AnalysesErrorCodes.NoSuchAccount);

        var id = IdOf(client, request.Key);
        var now = time.GetUtcNow();
        var analysis = await db.Analyses.Include(x => x.Contents).Include(x => x.Files).SingleOrDefaultAsync(x => x.SourceId == id, cancellationToken);
        if (analysis == null)
        {
            analysis = new()
            {
                Id = Guid.NewGuid(),
                SourceId = id,
                Title = request.Title,
                CreatedBy = client,
                CreatedAt = now,
            };
            db.Analyses.Add(analysis);
        }

        analysis.Title = request.Title.Trim();
        analysis.Artists = request.Artists.Select(x => x.Trim()).ToList();
        analysis.OwnerId = request.OwnerId;
        analysis.DurationSeconds = content.Timeline.Duration;
        analysis.UpdatedAt = now;

        // the files the content names: their keys in the music service's store
        var files = new Dictionary<string, string>();
        BlobRef Name(BlobRef blob)
        {
            if (!FileName().IsMatch(blob.Key) || !files.TryAdd(blob.Key, blob.ContentType) || string.IsNullOrWhiteSpace(blob.ContentType))
                throw ShelfException.BadRequest(AnalysesErrorCodes.BadFileName, new Dictionary<string, string> { ["name"] = blob.Key });

            return blob with { Key = BlobKeyOf(analysis.Id, blob.Key) };
        }

        AudioRef Audio(AudioRef audio) => (audio.Track == null) == (audio.Blob == null)
            ? throw ShelfException.BadRequest()
            : audio.Blob != null ? audio with { Blob = Name(audio.Blob) } : audio;

        var recording = content.Recording == null
            ? null
            : content.Recording with
            {
                Audio = Audio(content.Recording.Audio),
                Stems = content.Recording.Stems.Select(x => x with { Audio = Audio(x.Audio) }).ToList(),
            };
        var notes = content.Notes is { Midi: { } midi } ? content.Notes with { Midi = Name(midi) } : content.Notes;

        analysis.RecordingId = recording?.Audio.Track is { } track ? (await RecordingOf(track, now, cancellationToken)).Id : null;

        var kinds = new Dictionary<ContentKind, object?>
        {
            [ContentKind.Timeline] = content.Timeline,
            [ContentKind.Lyrics] = content.Lyrics,
            [ContentKind.Notes] = notes,
            [ContentKind.Score] = content.Score,
            [ContentKind.Chart] = content.Chart,
            [ContentKind.Recording] = recording,
        };

        foreach (var (kind, value) in kinds)
        {
            var name = KindName(kind);
            var row = analysis.Contents.SingleOrDefault(x => x.Kind == name);
            if (value == null)
            {
                if (row != null) db.AnalysisContents.Remove(row);
                continue;
            }

            var json = JsonSerializer.Serialize(value, value.GetType(), SourceV2Json.Options);
            if (row == null)
            {
                analysis.Contents.Add(new() { Kind = name, Content = json, UpdatedAt = now });
            }
            else
            {
                row.Content = json;
                row.UpdatedAt = now;
            }
        }

        // a file the content no longer names (or names in another type, to be put again) is no longer the analysis's,
        // and goes from the music service's store
        foreach (var file in analysis.Files.Where(x => !files.TryGetValue(x.Name, out var type) || type != x.ContentType).ToList())
        {
            db.AnalysisFiles.Remove(file);
            AnalysisBlobDeletions.Enqueue(db, file.BlobKey, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new()
        {
            Id = id,
            Files = files.Keys.Order(StringComparer.Ordinal).ToList(),
        };
    }

    /// <summary>
    /// Streams a file the analysis's content names into the music service's store.
    /// </summary>
    public async Task<PutAnalysisFileResponse> PutFileAsync(string client, string id, string name, string? contentType, Stream body, long? length, CancellationToken cancellationToken)
    {
        if (length > options.CurrentValue.MaxFileBytes)
            throw new ShelfException(StatusCodes.Status413PayloadTooLarge, AnalysesErrorCodes.FileTooLarge);

        var analysis = await db.Analyses.Include(x => x.Contents).Include(x => x.Files)
                           .SingleOrDefaultAsync(x => x.SourceId == id && x.CreatedBy == client, cancellationToken)
                       ?? throw ShelfException.NotFound(AnalysesErrorCodes.NoSuchAnalysis);

        var named = NamedFiles(analysis).GetValueOrDefault(name);
        if (named == null || contentType == null || !string.Equals(named, contentType.Split(';')[0].Trim(), StringComparison.OrdinalIgnoreCase))
            throw ShelfException.BadRequest(AnalysesErrorCodes.FileNotNamed, new Dictionary<string, string> { ["name"] = name });

        var key = BlobKeyOf(analysis.Id, name);

        // The music service writes the upload apart and makes it current at once, so the upload is not held; only making it
        // the analysis's is, below, under the key's lock that a deletion of the file takes too.
        AudioBlob stored;
        try
        {
            stored = await blobs.PutAsync(key, named, body, length, cancellationToken);
        }
        catch (AudioException exception)
        {
            throw exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? new ShelfException(StatusCodes.Status413PayloadTooLarge, AnalysesErrorCodes.FileTooLarge)
                : new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockBlobAsync(db, key, cancellationToken);

        // a deletion of the key (of a file dropped before the content named it again) that ran meanwhile took this upload
        AudioBlob? current;
        try
        {
            current = await blobs.GetInfoAsync(key, cancellationToken);
        }
        catch (AudioException)
        {
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        if (current == null || current.Sha256 != stored.Sha256)
            throw ShelfException.Conflict(AnalysesErrorCodes.FileReplaced, new Dictionary<string, string> { ["name"] = name });

        var file = analysis.Files.SingleOrDefault(x => x.Name == name);
        if (file == null)
        {
            file = new() { Name = name, BlobKey = key, ContentType = named, CreatedAt = time.GetUtcNow() };
            analysis.Files.Add(file);
        }

        file.BlobKey = key;
        file.ContentType = named;
        file.Size = stored.Bytes;
        file.CreatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new() { Name = name, Bytes = stored.Bytes };
    }

    /// <summary>
    /// The files of an analysis put so far, as the music service keeps them: their sizes and SHA-256s.
    /// </summary>
    public async Task<IReadOnlyList<AnalysisFileInfo>> GetFilesAsync(string client, string id, CancellationToken cancellationToken)
    {
        var analysis = await db.Analyses.AsNoTracking().Include(x => x.Files)
                           .SingleOrDefaultAsync(x => x.SourceId == id && x.CreatedBy == client, cancellationToken)
                       ?? throw ShelfException.NotFound(AnalysesErrorCodes.NoSuchAnalysis);

        var result = new List<AnalysisFileInfo>();
        foreach (var file in analysis.Files.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            AudioBlob? stored;
            try
            {
                stored = await blobs.GetInfoAsync(file.BlobKey, cancellationToken);
            }
            catch (AudioException)
            {
                throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
            }

            if (stored != null)
                result.Add(new() { Name = file.Name, ContentType = file.ContentType, Bytes = stored.Bytes, Sha256 = stored.Sha256 });
        }

        return result;
    }

    /// <summary>
    /// Removes an analysis that this client put; its files go from the music service's store.
    /// </summary>
    public async Task DeleteAsync(string client, string id, CancellationToken cancellationToken)
    {
        var analysis = await db.Analyses.Include(x => x.Files)
                           .SingleOrDefaultAsync(x => x.SourceId == id && x.CreatedBy == client, cancellationToken)
                       ?? throw ShelfException.NotFound(AnalysesErrorCodes.NoSuchAnalysis);

        var now = time.GetUtcNow();
        foreach (var file in analysis.Files)
            AnalysisBlobDeletions.Enqueue(db, file.BlobKey, now);

        db.Analyses.Remove(analysis);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes the analyses of a person's own files (when the profile is removed), in the caller's transaction: their
    /// files go from the music service's store, by deletions added to the context's changes (the caller saves them).
    /// </summary>
    public static async Task RemoveOwnedAsync(ShelfDbContext db, Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var keys = await db.AnalysisFiles.Where(x => x.Analysis.OwnerId == accountId).Select(x => x.BlobKey).ToListAsync(cancellationToken);
        foreach (var key in keys)
            AnalysisBlobDeletions.Enqueue(db, key, now);

        await db.Analyses.Where(x => x.OwnerId == accountId).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Holds a file's key for the transaction: its upload and its deletion do not overlap.
    /// </summary>
    internal static Task LockBlobAsync(ShelfDbContext db, string key, CancellationToken cancellationToken)
        => db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

    /// <summary>
    /// The files an analysis's content names, with their content types.
    /// </summary>
    private static Dictionary<string, string> NamedFiles(Analysis analysis)
    {
        var result = new Dictionary<string, string>();
        void Add(BlobRef? blob)
        {
            if (blob != null) result[blob.Key[(blob.Key.LastIndexOf('/') + 1)..]] = blob.ContentType;
        }

        if (analysis.Contents.SingleOrDefault(x => x.Kind == KindName(ContentKind.Recording)) is { } recordingRow)
        {
            var recording = JsonSerializer.Deserialize<Recording>(recordingRow.Content, SourceV2Json.Options)!;
            Add(recording.Audio.Blob);
            foreach (var stem in recording.Stems) Add(stem.Audio.Blob);
        }

        if (analysis.Contents.SingleOrDefault(x => x.Kind == KindName(ContentKind.Notes)) is { } notesRow)
            Add(JsonSerializer.Deserialize<NotesMidi>(notesRow.Content, SourceV2Json.Options)!.Midi);

        return result;
    }

    /// <summary>
    /// Of the notes, the MIDI file only (the notes themselves are many).
    /// </summary>
    private sealed record NotesMidi(BlobRef? Midi);

    private async Task<Database.Model.Recording> RecordingOf(MusicServiceTrack track, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var service = track.Service switch
        {
            Source.Api.Model.V2.Contents.MusicService.Yandex => Database.Model.MusicService.Yandex,
            Source.Api.Model.V2.Contents.MusicService.Youtube => Database.Model.MusicService.Youtube,
            _ => throw ShelfException.BadRequest(),
        };

        var recording = await db.Recordings.SingleOrDefaultAsync(x => x.Service == service && x.ServiceId == track.Id, cancellationToken);
        if (recording == null)
        {
            recording = new() { Id = Guid.NewGuid(), Service = service, ServiceId = track.Id, UpdatedAt = now };
            db.Recordings.Add(recording);
        }

        return recording;
    }
}
