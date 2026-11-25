using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recording = HarmonyDB.Source.Api.Model.V2.Contents.Recording;

namespace HarmonyDB.Shelf.Api.Areas.Analyses.Services;

/// <summary>
/// The shelf's own analyses as a source (the source contract, version 2). An analysis is open to the caller when it is
/// open to all, or when it is of the caller's own file. Its address, the "link to the source", is on the public page,
/// by the source's own address template (<see cref="AnalysesOptions.PublicUriTemplate"/>).
/// </summary>
public sealed class AnalysesSource(ShelfDbContext db, IOptionsMonitor<AnalysesOptions> options)
{
    /// <summary>
    /// The most items a search gives.
    /// </summary>
    public const int SearchLimit = 50;

    private AnalysesOptions Options => options.CurrentValue.IsConfigured
        ? options.CurrentValue
        : throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

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
    /// The analyses open to the caller whose title or artists have every word of the query.
    /// </summary>
    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var words = request.Query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return new() { Headers = [], Failures = [] };

        var query = Open(request.Caller);
        foreach (var word in words)
        {
            var pattern = $"%{word.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            query = query.Where(x => EF.Functions.ILike(x.Title, pattern, @"\") || x.Artists.Any(a => EF.Functions.ILike(a, pattern, @"\")));
        }

        var found = await query.OrderBy(x => x.Title).ThenBy(x => x.SourceId).Take(SearchLimit).ToListAsync(cancellationToken);
        return new()
        {
            Headers = await HeadersAsync(found, cancellationToken),
            Failures = [],
        };
    }

    public async Task<GetHeadersResponse> GetHeadersAsync(GetHeadersRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();
        var found = await Open(request.Caller).Where(x => ids.Contains(x.SourceId)).ToListAsync(cancellationToken);
        var headers = (await HeadersAsync(found, cancellationToken)).ToDictionary(x => x.Id);

        return new()
        {
            Headers = request.Ids
                .Select(id => headers.TryGetValue(id, out var header)
                    ? new HeaderAnswer { Id = id, State = ItemState.Open, Header = header }
                    : new HeaderAnswer { Id = id, State = ItemState.NotOpen })
                .ToList(),
        };
    }

    public async Task<GetContentResponse> GetContentAsync(GetContentRequest request, CancellationToken cancellationToken)
        => (await ContentsAsync(request.Caller, [request.Id], request.Kinds, cancellationToken)).Single();

    /// <summary>
    /// The content of several analyses, read in one query each for the analyses, their headers and their contents.
    /// </summary>
    public async Task<GetContentsResponse> GetContentsAsync(GetContentsRequest request, CancellationToken cancellationToken)
        => new() { Items = await ContentsAsync(request.Caller, request.Ids, request.Kinds, cancellationToken) };

    private async Task<IReadOnlyList<GetContentResponse>> ContentsAsync(Caller caller, IReadOnlyList<string> ids, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var found = await Open(caller).Where(x => ids.Contains(x.SourceId)).ToListAsync(cancellationToken);
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
            // the kinds asked that the analysis has (a kind that names files is had once they are stored)
            T? Get<T>(ContentKind kind) where T : class
                => header.Kinds.Contains(kind) && kinds.Contains(kind) && rows.TryGetValue((analysis.Id, AnalysisStore.KindName(kind)), out var json)
                    ? JsonSerializer.Deserialize<T>(json, SourceV2Json.Options)
                    : null;

            // every kind as it was put
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
    /// No address is resolved: the analyses' addresses on the public page are given in their headers only.
    /// </summary>
    public ResolveResponse Resolve(ResolveRequest request) => new()
    {
        Items = request.Uris.Select(x => new ResolvedUri { Uri = x, State = ResolveState.NotResolved }).ToList(),
    };

    /// <summary>
    /// The source's picture, when it is the one of this name.
    /// </summary>
    public async Task<(byte[] content, string contentType)?> GetIconAsync(GetIconRequest request, CancellationToken cancellationToken)
    {
        var settings = Options;
        if (string.IsNullOrWhiteSpace(settings.IconName) || request.Name != settings.IconName)
            return null;

        return (await File.ReadAllBytesAsync(settings.IconFile!, cancellationToken), settings.IconContentType!);
    }

    /// <summary>
    /// Answers when the source works: it is configured and its database answers.
    /// </summary>
    public async Task PingAsync(CancellationToken cancellationToken)
    {
        _ = Options;
        if (!await db.Database.CanConnectAsync(cancellationToken))
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
    }

    private IQueryable<Analysis> Open(Caller caller)
    {
        _ = Options;
        return db.Analyses.AsNoTracking().Where(x => x.OwnerId == null || x.OwnerId == caller.AccountId);
    }

    /// <summary>
    /// The headers of analyses, in their order: their kinds, and what their timelines state at the start. A kind that
    /// names files of the analysis (the recording and its instruments, the notes' MIDI file) is had only once every file
    /// it names is put: until then it is not given at all.
    /// </summary>
    public async Task<IReadOnlyList<Header>> HeadersAsync(IReadOnlyList<Analysis> analyses, CancellationToken cancellationToken)
    {
        var ids = analyses.Select(x => x.Id).ToList();
        var kinds = (await db.Database.SqlQuery<ReadyKind>($$"""
                SELECT c.analysis_id, c.kind
                FROM analysis_contents c
                WHERE c.analysis_id = ANY({{ids}}) AND NOT EXISTS (
                    SELECT 1
                    FROM (SELECT jsonb_path_query(c.content, 'lax $.audio.blob.key') AS key
                          UNION ALL SELECT jsonb_path_query(c.content, 'lax $.stems[*].audio.blob.key')
                          UNION ALL SELECT jsonb_path_query(c.content, 'lax $.midi.key')) named
                    WHERE jsonb_typeof(named.key) = 'string' AND NOT EXISTS (
                        SELECT 1 FROM analysis_files f WHERE f.analysis_id = c.analysis_id AND f.blob_key = named.key #>> '{}'))
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
        var settings = Options;
        return analyses.Select(x =>
        {
            var attribute = attributes.GetValueOrDefault(x.Id);
            return new Header
            {
                Id = x.SourceId,
                Source = label with { Uri = settings.PublicUri(x.SourceId) },
                Title = x.Title,
                Artists = x.Artists,
                Kinds = Enum.GetValues<ContentKind>().Where(k => kinds[x.Id].Contains(AnalysisStore.KindName(k))).ToList(),
                Attributes = new()
                {
                    Key = attribute?.Key,
                    Bpm = attribute?.Bpm,
                    TimeSignature = attribute?.TimeSignature,
                },
                IsPublic = x.OwnerId == null,
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
