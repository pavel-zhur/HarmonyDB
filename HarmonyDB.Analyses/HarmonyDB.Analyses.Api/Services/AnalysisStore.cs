using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyDB.Analyses.Api.Models;
using HarmonyDB.Analyses.Database;
using HarmonyDB.Analyses.Database.Model;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Analyses.Api.Services;

/// <summary>
/// Puts analyses in and takes them away: their content in the source contract's kinds, kept as it is put, and their
/// files, kept in the analyses' folder (<see cref="AnalysisFolder"/>).
/// </summary>
public sealed partial class AnalysisStore(AnalysesDatabase db, AnalysisFolder folder, TimeProvider time)
{
    /// <summary>
    /// The prefix of the analyses' ids as the source's items.
    /// </summary>
    public const string IdPrefix = "ha-";

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,99}$")]
    private static partial Regex FileName();

    /// <summary>
    /// The id of a client's analysis: made of the client's name and its key, so the same analysis put again is found.
    /// </summary>
    public static string IdOf(string client, string key)
        => IdPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{client}\n{key}")))[..32].ToLowerInvariant();

    /// <summary>
    /// The content kinds as the source contract's wire format writes them ("timeline", "lyrics" …).
    /// </summary>
    public static string KindName(ContentKind kind) => JsonSerializer.Serialize(kind, SourceV2Json.Options).Trim('"');

    public async Task<PutAnalysisResponse> PutAsync(string client, PutAnalysisRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || request.Key.Length > 200 || string.IsNullOrWhiteSpace(request.Title)
            || request.Artists == null || request.Artists.Any(string.IsNullOrWhiteSpace) || request.Content == null
            || request.OrderedBy is { Length: 0 or > 200 }
            || request.MadeFrom is { } madeFrom && (string.IsNullOrWhiteSpace(madeFrom.Service) || madeFrom.Service != madeFrom.Service.ToLowerInvariant() || string.IsNullOrWhiteSpace(madeFrom.Id)))
            throw new AnalysesException(StatusCodes.Status400BadRequest, AnalysesErrorCodes.BadRequest);

        var content = request.Content;
        if (content.Timeline == null)
            throw new AnalysesException(StatusCodes.Status400BadRequest, AnalysesErrorCodes.TimelineMissing);

        // the sheet is kept as it is given: it is only to be one that can be read and drawn
        if (content.Sheet != null)
        {
            try
            {
                content.Sheet.Root.AsHtml(new RepresentationSettings());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new AnalysesException(StatusCodes.Status400BadRequest, AnalysesErrorCodes.BadSheet);
            }
        }

        // the files the content names, with the kind that names them and their content types
        var files = new Dictionary<string, (string Kind, string ContentType)>(StringComparer.Ordinal);
        void Name(FileRef? file, ContentKind kind)
        {
            if (file == null) return;
            if (file.Name == null || !FileName().IsMatch(file.Name) || string.IsNullOrWhiteSpace(file.ContentType) || !files.TryAdd(file.Name, (KindName(kind), file.ContentType)))
                throw new AnalysesException(StatusCodes.Status400BadRequest, AnalysesErrorCodes.BadFileName, file.Name);
        }

        void Audio(AudioRef? audio)
        {
            if (audio == null || (audio.Track == null) == (audio.File == null)
                || audio.Track is { } track && (string.IsNullOrWhiteSpace(track.Service) || track.Service != track.Service.ToLowerInvariant() || string.IsNullOrWhiteSpace(track.Id)))
                throw new AnalysesException(StatusCodes.Status400BadRequest, AnalysesErrorCodes.BadAudio);

            Name(audio.File, ContentKind.Recording);
        }

        if (content.Recording != null)
        {
            Audio(content.Recording.Audio);
            foreach (var stem in content.Recording.Stems ?? []) Audio(stem?.Audio);
        }

        Name(content.Notes?.Midi, ContentKind.Notes);

        var id = IdOf(client, request.Key);
        var now = time.GetUtcNow();
        var dropped = new List<(Guid analysisId, string storedName)>();

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await LockAsync(id, cancellationToken);

            var analysis = await db.Analyses.Include(x => x.Contents).Include(x => x.Files).SingleOrDefaultAsync(x => x.SourceId == id, cancellationToken);
            if (analysis == null)
            {
                analysis = new()
                {
                    Id = Guid.NewGuid(),
                    SourceId = id,
                    CreatedBy = client,
                    Title = request.Title,
                    CreatedAt = now,
                };
                db.Analyses.Add(analysis);
            }

            analysis.Title = request.Title.Trim();
            analysis.Artists = request.Artists.Select(x => x.Trim()).ToList();
            analysis.OrderedBy = request.OrderedBy;
            analysis.MadeFromService = request.MadeFrom?.Service;
            analysis.MadeFromId = request.MadeFrom?.Id;
            analysis.UpdatedAt = now;

            var kinds = new Dictionary<ContentKind, object?>
            {
                [ContentKind.Sheet] = content.Sheet,
                [ContentKind.Timeline] = content.Timeline,
                [ContentKind.Lyrics] = content.Lyrics,
                [ContentKind.Notes] = content.Notes,
                [ContentKind.Score] = content.Score,
                [ContentKind.Chart] = content.Chart,
                [ContentKind.Recording] = content.Recording,
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

            // a file the content no longer names (or names otherwise, to be put again) is no longer the analysis's
            foreach (var file in analysis.Files.Where(x => files.GetValueOrDefault(x.Name) != (x.Kind, x.ContentType)).ToList())
            {
                analysis.Files.Remove(file);
                db.AnalysisFiles.Remove(file);
                if (file.StoredName != null)
                    dropped.Add((analysis.Id, file.StoredName));
            }

            // a file it newly names is written down, to be uploaded
            foreach (var (name, named) in files.Where(x => analysis.Files.All(f => f.Name != x.Key)))
                analysis.Files.Add(new() { Name = name, Kind = named.Kind, ContentType = named.ContentType });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var (analysisId, storedName) in dropped)
            folder.Delete(analysisId, storedName);

        return new()
        {
            Id = id,
            Files = files.Keys.Order(StringComparer.Ordinal).ToList(),
        };
    }

    /// <summary>
    /// Keeps a file the analysis's content names: written to a new file of the folder first, then made the analysis's.
    /// </summary>
    public async Task<AnalysisFileInfo> PutFileAsync(string client, string id, string name, string? contentType, Stream body, long? length, long maxBytes, CancellationToken cancellationToken)
    {
        if (length > maxBytes)
            throw new AnalysesException(StatusCodes.Status413PayloadTooLarge, AnalysesErrorCodes.FileTooLarge, name);

        var analysis = await db.Analyses.AsNoTracking().Include(x => x.Files)
                           .SingleOrDefaultAsync(x => x.SourceId == id && x.CreatedBy == client, cancellationToken)
                       ?? throw new AnalysesException(StatusCodes.Status404NotFound, AnalysesErrorCodes.NoSuchAnalysis);

        var named = analysis.Files.SingleOrDefault(x => x.Name == name);
        if (named == null || contentType == null || !string.Equals(named.ContentType, contentType.Split(';')[0].Trim(), StringComparison.OrdinalIgnoreCase))
            throw new AnalysesException(StatusCodes.Status400BadRequest, AnalysesErrorCodes.FileNotNamed, name);

        var written = await folder.WriteAsync(analysis.Id, name, body, maxBytes, cancellationToken)
                      ?? throw new AnalysesException(StatusCodes.Status413PayloadTooLarge, AnalysesErrorCodes.FileTooLarge, name);

        string? replaced = null;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(id, cancellationToken);

            // the content may have been put again meanwhile
            var file = await db.AnalysisFiles.SingleOrDefaultAsync(x => x.AnalysisId == analysis.Id && x.Name == name, cancellationToken);
            if (file == null || (file.Kind, file.ContentType) != (named.Kind, named.ContentType))
                throw new AnalysesException(StatusCodes.Status409Conflict, AnalysesErrorCodes.FileDropped, name);

            replaced = file.StoredName;
            file.StoredName = written.StoredName;
            file.Size = written.Size;
            file.Sha256 = written.Sha256;
            file.StoredAt = time.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            folder.Delete(analysis.Id, written.StoredName);
            throw;
        }

        if (replaced != null)
            folder.Delete(analysis.Id, replaced);

        return new() { Name = name, ContentType = named.ContentType, Bytes = written.Size, Sha256 = written.Sha256 };
    }

    /// <summary>
    /// The files of an analysis kept so far.
    /// </summary>
    public async Task<IReadOnlyList<AnalysisFileInfo>> GetFilesAsync(string client, string id, CancellationToken cancellationToken)
    {
        var analysis = await db.Analyses.AsNoTracking().Include(x => x.Files)
                           .SingleOrDefaultAsync(x => x.SourceId == id && x.CreatedBy == client, cancellationToken)
                       ?? throw new AnalysesException(StatusCodes.Status404NotFound, AnalysesErrorCodes.NoSuchAnalysis);

        return analysis.Files
            .Where(x => x.StoredName != null)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => new AnalysisFileInfo { Name = x.Name, ContentType = x.ContentType, Bytes = x.Size!.Value, Sha256 = x.Sha256! })
            .ToList();
    }

    /// <summary>
    /// Takes away an analysis that this client put, with its files.
    /// </summary>
    public async Task DeleteAsync(string client, string id, CancellationToken cancellationToken)
    {
        Guid analysisId;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await LockAsync(id, cancellationToken);
            var analysis = await db.Analyses.SingleOrDefaultAsync(x => x.SourceId == id && x.CreatedBy == client, cancellationToken)
                           ?? throw new AnalysesException(StatusCodes.Status404NotFound, AnalysesErrorCodes.NoSuchAnalysis);

            analysisId = analysis.Id;
            db.Analyses.Remove(analysis);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        folder.DeleteAll(analysisId);
    }

    /// <summary>
    /// Holds the analysis of this id for the transaction: its content and its files change one at a time.
    /// </summary>
    private Task LockAsync(string id, CancellationToken cancellationToken)
        => db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id}, 0))", cancellationToken);
}
