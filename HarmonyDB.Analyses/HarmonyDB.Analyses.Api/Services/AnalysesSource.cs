using System.Text.Json;
using HarmonyDB.Analyses.Api.Models;
using HarmonyDB.Analyses.Database;
using HarmonyDB.Analyses.Database.Model;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recording = HarmonyDB.Source.Api.Model.V2.Contents.Recording;

namespace HarmonyDB.Analyses.Api.Services;

/// <summary>
/// The analyses as a source of the contract, version 2. Every analysis is open to every caller: the caller is taken as
/// the contract requires and decides nothing here, nor does an address it came by. An item's address is its public page
/// (<see cref="AnalysesOptions.PublicUriTemplate"/>), and that address resolves to it. A kind that names files of the
/// analysis (the recording and its instruments, the notes' MIDI file) is had only once every file it names is kept;
/// until then it is not given, and neither are its files.
/// </summary>
public sealed class AnalysesSource(AnalysesDatabase db, AnalysisFolder folder, IOptions<AnalysesOptions> options)
{
    /// <summary>
    /// The most items a search gives.
    /// </summary>
    public const int SearchLimit = 50;

    private AnalysesOptions Options => options.Value;

    /// <summary>
    /// How the source labels its items, the same for every caller.
    /// </summary>
    public SourceLabel Label => new()
    {
        Key = Options.SourceKey!,
        Title = Options.SourceTitle!,
        Icon = string.IsNullOrWhiteSpace(Options.IconName) ? null : Options.IconName,
    };

    /// <summary>
    /// The analyses whose title or artists have every word of the query, by title.
    /// </summary>
    public async Task<SearchResponse> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return new() { Headers = [], Failures = [] };

        var analyses = db.Analyses.AsNoTracking();
        foreach (var word in words)
        {
            var pattern = $"%{word.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            analyses = analyses.Where(x => EF.Functions.ILike(x.Title, pattern, @"\") || x.Artists.Any(a => EF.Functions.ILike(a, pattern, @"\")));
        }

        var found = await analyses.OrderBy(x => x.Title).ThenBy(x => x.SourceId).Take(SearchLimit).ToListAsync(cancellationToken);
        return new()
        {
            Headers = await HeadersAsync(found, cancellationToken),
            Failures = [],
        };
    }

    public async Task<GetHeadersResponse> GetHeadersAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var headers = (await HeadersAsync(await FindAsync(ids, cancellationToken), cancellationToken)).ToDictionary(x => x.Id);
        return new()
        {
            Headers = ids
                .Select(id => headers.TryGetValue(id, out var header)
                    ? new HeaderAnswer { Id = id, State = ItemState.Open, Header = header }
                    : new HeaderAnswer { Id = id, State = ItemState.NotOpen })
                .ToList(),
        };
    }

    /// <summary>
    /// The content of analyses, read in one query each for the analyses, their kinds and their contents.
    /// </summary>
    public async Task<IReadOnlyList<GetContentResponse>> GetContentsAsync(IReadOnlyList<string> ids, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var found = await FindAsync(ids, cancellationToken);
        var headers = (await HeadersAsync(found, cancellationToken)).ToDictionary(x => x.Id);

        var analysisIds = found.Select(x => x.Id).ToList();
        var names = kinds.Select(AnalysisStore.KindName).ToList();
        var rows = (await db.AnalysisContents
                .Where(x => analysisIds.Contains(x.AnalysisId) && names.Contains(x.Kind))
                .Select(x => new { x.AnalysisId, x.Kind, x.Content })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.AnalysisId, x.Kind), x => x.Content);
        var bySourceId = found.ToDictionary(x => x.SourceId);

        return ids.Select(id =>
        {
            if (!bySourceId.TryGetValue(id, out var analysis))
                return new GetContentResponse { Id = id, State = ItemState.NotOpen };

            var header = headers[id];

            // the kinds asked that the analysis has, every one as it was put
            T? Get<T>(ContentKind kind) where T : class
                => header.Kinds.Contains(kind) && kinds.Contains(kind) && rows.TryGetValue((analysis.Id, AnalysisStore.KindName(kind)), out var json)
                    ? JsonSerializer.Deserialize<T>(json, SourceV2Json.Options)
                    : null;

            return new GetContentResponse
            {
                Id = id,
                State = ItemState.Open,
                Header = header,
                Content = new()
                {
                    Sheet = Get<SheetContent>(ContentKind.Sheet),
                    Timeline = Get<Timeline>(ContentKind.Timeline),
                    Lyrics = Get<Lyrics>(ContentKind.Lyrics),
                    Notes = Get<Notes>(ContentKind.Notes),
                    Score = Get<Score>(ContentKind.Score),
                    Chart = Get<Chart>(ContentKind.Chart),
                    Recording = Get<Recording>(ContentKind.Recording),
                },
            };
        }).ToList();
    }

    /// <summary>
    /// An address resolves when it is the public page of an analysis there is.
    /// </summary>
    public async Task<ResolveResponse> ResolveAsync(IReadOnlyList<Uri> uris, CancellationToken cancellationToken)
    {
        var ids = uris.Select(Options.IdOfPublicUri).ToList();
        var known = (await FindAsync(ids.OfType<string>().Distinct().ToList(), cancellationToken)).Select(x => x.SourceId).ToHashSet();

        return new()
        {
            Items = uris.Select((uri, i) => ids[i] is { } id && known.Contains(id)
                    ? new ResolvedUri { Uri = uri, State = ResolveState.Resolved, Id = id }
                    : new ResolvedUri { Uri = uri, State = ResolveState.NotResolved })
                .ToList(),
        };
    }

    /// <summary>
    /// A file of an analysis, when its content as given names it: the file's path, its content type and its SHA-256.
    /// </summary>
    /// <returns>Null when there is no such analysis or file, or the kind that names it is not had yet.</returns>
    public async Task<(string path, string contentType, string sha256, DateTimeOffset storedAt)?> GetFileAsync(string id, string name, CancellationToken cancellationToken)
    {
        var analysis = await db.Analyses.AsNoTracking().Include(x => x.Contents).Include(x => x.Files)
            .SingleOrDefaultAsync(x => x.SourceId == id, cancellationToken);
        if (analysis == null) return null;

        var header = (await HeadersAsync([analysis], cancellationToken)).Single();
        var given = analysis.Contents.Where(x => header.Kinds.Any(k => AnalysisStore.KindName(k) == x.Kind));
        var file = analysis.Files.SingleOrDefault(x => x.Name == name);
        if (file == null || AnalysisStore.NamedFiles(given).GetValueOrDefault(name) != file.ContentType)
            return null;

        return (folder.PathOf(analysis.Id, file.StoredName), file.ContentType, file.Sha256, file.StoredAt);
    }

    /// <summary>
    /// The source's picture, when it is the one of this name.
    /// </summary>
    public async Task<(byte[] content, string contentType)?> GetIconAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Options.IconName) || name != Options.IconName)
            return null;

        return (await File.ReadAllBytesAsync(Options.IconFile!, cancellationToken), Options.IconContentType!);
    }

    /// <summary>
    /// Whether the source works: its database answers.
    /// </summary>
    public Task<bool> PingAsync(CancellationToken cancellationToken) => db.Database.CanConnectAsync(cancellationToken);

    private async Task<List<Analysis>> FindAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        return await db.Analyses.AsNoTracking().Where(x => distinct.Contains(x.SourceId)).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The headers of analyses, in their order: their kinds, and what their timelines state at the start.
    /// </summary>
    private async Task<IReadOnlyList<Header>> HeadersAsync(IReadOnlyList<Analysis> analyses, CancellationToken cancellationToken)
    {
        var ids = analyses.Select(x => x.Id).ToList();
        var kinds = (await db.Database.SqlQuery<ReadyKind>($$"""
                SELECT c.analysis_id, c.kind
                FROM analysis_contents c
                WHERE c.analysis_id = ANY({{ids}}) AND NOT EXISTS (
                    SELECT 1
                    FROM (SELECT jsonb_path_query(c.content, 'lax $.audio.file.name') AS name
                          UNION ALL SELECT jsonb_path_query(c.content, 'lax $.stems[*].audio.file.name')
                          UNION ALL SELECT jsonb_path_query(c.content, 'lax $.midi.name')) named
                    WHERE jsonb_typeof(named.name) = 'string' AND NOT EXISTS (
                        SELECT 1 FROM analysis_files f WHERE f.analysis_id = c.analysis_id AND f.name = named.name #>> '{}'))
                """).ToListAsync(cancellationToken))
            .ToLookup(x => x.AnalysisId, x => x.Kind);
        var timeline = AnalysisStore.KindName(ContentKind.Timeline);
        var attributes = (await db.Database.SqlQuery<TimelineAttributes>($$"""
                SELECT analysis_id AS analysis_id,
                       jsonb_path_query_first(content, '$.keys[*].key ? (@ != null)') #>> '{}' AS key,
                       jsonb_path_query_first(content, '$.bars[*].timeSignature ? (@ != "free")') #>> '{}' AS time_signature,
                       (jsonb_path_query_first(content, '$.bars[*].tempo.bpm') #>> '{}')::double precision AS bpm
                FROM analysis_contents
                WHERE kind = {{timeline}} AND analysis_id = ANY({{ids}})
                """).ToListAsync(cancellationToken))
            .ToDictionary(x => x.AnalysisId);

        var label = Label;
        return analyses.Select(x =>
        {
            var attribute = attributes.GetValueOrDefault(x.Id);
            return new Header
            {
                Id = x.SourceId,
                Source = label,
                Address = Options.PublicUri(x.SourceId),
                Title = x.Title,
                Artists = x.Artists,
                Kinds = Enum.GetValues<ContentKind>().Where(k => kinds[x.Id].Contains(AnalysisStore.KindName(k))).ToList(),
                Attributes = new()
                {
                    Key = attribute?.Key,
                    Bpm = attribute?.Bpm,
                    TimeSignature = attribute?.TimeSignature,
                },
                IsPublic = true,
                MadeFrom = x is { MadeFromService: { } service, MadeFromId: { } track } ? new() { Service = service, Id = track } : null,
            };
        }).ToList();
    }

    private sealed class ReadyKind
    {
        public Guid AnalysisId { get; set; }

        public string Kind { get; set; } = string.Empty;
    }

    private sealed class TimelineAttributes
    {
        public Guid AnalysisId { get; set; }

        public string? Key { get; set; }

        public string? TimeSignature { get; set; }

        public double? Bpm { get; set; }
    }
}
