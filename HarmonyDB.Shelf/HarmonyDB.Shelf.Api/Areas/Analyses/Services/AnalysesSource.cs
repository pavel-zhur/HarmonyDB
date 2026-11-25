using System.Text.Json;
using HarmonyDB.Shelf.Api.Areas.Analyses.Services.Sheets;
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
/// open to all, or when it is of the caller's own file.
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
    {
        var analysis = await Open(request.Caller).SingleOrDefaultAsync(x => x.SourceId == request.Id, cancellationToken);
        if (analysis == null)
            return new() { Id = request.Id, State = ItemState.NotOpen };

        var header = (await HeadersAsync([analysis], cancellationToken)).Single();

        // the sheet is made of the timeline and the words
        var asked = request.Kinds.Where(header.Kinds.Contains).ToHashSet();
        var read = asked.Contains(ContentKind.Sheet) ? asked.Union([ContentKind.Timeline, ContentKind.Lyrics]) : asked;
        var names = read.Select(AnalysisStore.KindName).ToList();
        var rows = await db.AnalysisContents
            .Where(x => x.AnalysisId == analysis.Id && names.Contains(x.Kind))
            .ToDictionaryAsync(x => x.Kind, x => x.Content, cancellationToken);

        T? Get<T>(ContentKind kind) where T : class
            => rows.TryGetValue(AnalysisStore.KindName(kind), out var json) ? JsonSerializer.Deserialize<T>(json, SourceV2Json.Options) : null;

        var timeline = Get<Timeline>(ContentKind.Timeline);
        var lyrics = Get<Lyrics>(ContentKind.Lyrics);

        return new()
        {
            Id = request.Id,
            State = ItemState.Open,
            Header = header,
            Content = new()
            {
                Sheet = asked.Contains(ContentKind.Sheet) ? new SheetContent { Root = SheetBuilder.Build(timeline!, lyrics) } : null,
                // its sections named as the sheet's headers name them
                Timeline = asked.Contains(ContentKind.Timeline) ? SheetBuilder.WithSectionNames(timeline!) : null,
                Lyrics = asked.Contains(ContentKind.Lyrics) ? lyrics : null,
                Notes = asked.Contains(ContentKind.Notes) ? Get<Notes>(ContentKind.Notes) : null,
                Score = asked.Contains(ContentKind.Score) ? Get<Score>(ContentKind.Score) : null,
                Chart = asked.Contains(ContentKind.Chart) ? Get<Chart>(ContentKind.Chart) : null,
                Recording = asked.Contains(ContentKind.Recording) ? Get<Recording>(ContentKind.Recording) : null,
            },
        };
    }

    /// <summary>
    /// The analyses have no addresses of their own: none is resolved.
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
        return analyses.Select(x =>
        {
            var had = Enum.GetValues<ContentKind>().Where(k => kinds[x.Id].Contains(AnalysisStore.KindName(k))).ToList();
            var attribute = attributes.GetValueOrDefault(x.Id);
            return new Header
            {
                Id = x.SourceId,
                Source = label,
                Title = x.Title,
                Artists = x.Artists,
                Kinds = had.Contains(ContentKind.Timeline) ? had.Prepend(ContentKind.Sheet).ToList() : had,
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
