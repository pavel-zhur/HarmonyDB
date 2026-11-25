using System.Text.RegularExpressions;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using OneShelf.Collectives.Api.Client;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Sources.Self.Api.Models;

namespace OneShelf.Sources.Self.Api.Services.V2;

/// <summary>
/// The own chords (the collectives) as a source of the contract, version 2.
/// <list type="bullet">
/// <item>The id of a collective is <c>mj-{its id}</c>: it stays the same through the collective's versions.</item>
/// <item>A public collective is open to every caller and found by every search. A club one (there are no clubs in the
/// shelf; the club's people keep its collectives in shared playlists) is open to every caller who holds its id, and
/// found by the searches of its author only. A private one is open to, and found by, its author only. The author is
/// the caller whose account the shelf links to the Telegram user who made it (<see cref="ShelfAccountsClient"/>).
/// A deleted collective is not open. The caller's keys open nothing here.</item>
/// <item>For a caller who is a person, the shelf is asked who it is before anything else, so what is answered does not
/// depend on what exists. When the shelf does not answer, a search gives what is open to all and reports this source
/// as failed, and an id that is not open to all is unavailable, whether it exists or not.</item>
/// <item>The icon's name begins with <see cref="IdPrefix"/>: the index routes it to this source as it routes an id.</item>
/// </list>
/// </summary>
public partial class SelfV2Source
{
    public const string IdPrefix = "mj-";

    private readonly CollectivesApiClient _collectivesApiClient;
    private readonly ShelfAccountsClient _shelfAccountsClient;
    private readonly StructureParser _structureParser;
    private readonly MetadataBuilder _metadataBuilder;
    private readonly SelfApiOptions _selfOptions;
    private readonly SelfV2Options _options;

    public SelfV2Source(CollectivesApiClient collectivesApiClient, ShelfAccountsClient shelfAccountsClient, StructureParser structureParser, MetadataBuilder metadataBuilder, IOptions<SelfApiOptions> selfOptions, IOptions<SelfV2Options> options)
    {
        _collectivesApiClient = collectivesApiClient;
        _shelfAccountsClient = shelfAccountsClient;
        _structureParser = structureParser;
        _metadataBuilder = metadataBuilder;
        _selfOptions = selfOptions.Value;
        _options = options.Value;
    }

    public static string ToId(Guid collectiveId) => $"{IdPrefix}{collectiveId:D}";

    public static Guid? FromId(string id)
        => id.StartsWith(IdPrefix, StringComparison.Ordinal) && Guid.TryParseExact(id[IdPrefix.Length..], "D", out var collectiveId) && ToId(collectiveId) == id
            ? collectiveId
            : null;

    /// <summary>
    /// The collectives the caller finds whose title or authors contain every word of the query, by title.
    /// </summary>
    public async Task<SearchResponse> Search(Caller caller, string query, CancellationToken cancellationToken)
    {
        var viewer = await Viewer.Create(caller, _shelfAccountsClient, cancellationToken);
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var headers = (await _collectivesApiClient.ListAll()).Versions
            .Where(viewer.Finds)
            .Where(version =>
            {
                var text = string.Join(' ', version.Collective.Authors.Prepend(version.Collective.Title));
                return words.All(w => text.Contains(w, StringComparison.CurrentCultureIgnoreCase));
            })
            .OrderBy(x => x.Collective.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToHeader)
            .ToList();

        return new()
        {
            Headers = headers,
            Failures = viewer.IsUnknownPerson
                ? [new() { Source = _selfOptions.SourceName, Reason = SearchFailureReason.Unavailable }]
                : [],
        };
    }

    public async Task<GetHeadersResponse> GetHeaders(Caller caller, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var viewer = await Viewer.Create(caller, _shelfAccountsClient, cancellationToken);
        var found = await Get(ids.Select(FromId));

        return new()
        {
            Headers = ids
                .Select(id =>
                {
                    var version = FromId(id) is { } collectiveId ? found.GetValueOrDefault(collectiveId) : null;
                    var state = viewer.StateOf(version);
                    return new HeaderAnswer
                    {
                        Id = id,
                        State = state,
                        Header = state == ItemState.Open ? ToHeader(version!) : null,
                    };
                })
                .ToList(),
        };
    }

    public async Task<GetContentResponse> GetContent(Caller caller, string id, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var viewer = await Viewer.Create(caller, _shelfAccountsClient, cancellationToken);
        var collectiveId = FromId(id);
        var version = collectiveId == null ? null : (await Get([collectiveId])).GetValueOrDefault(collectiveId.Value);
        var state = viewer.StateOf(version);
        if (state != ItemState.Open)
        {
            return new()
            {
                Id = id,
                State = state,
            };
        }

        return new()
        {
            Id = id,
            State = state,
            Header = ToHeader(version!),
            Content = new()
            {
                Sheet = kinds.Contains(ContentKind.Sheet)
                    ? new SheetContent
                    {
                        Root = _structureParser.ContentToHtml(version!.Collective),
                        AuthorComment = string.IsNullOrWhiteSpace(version.Collective.Description) ? null : version.Collective.Description,
                    }
                    : null,
            },
        };
    }

    /// <summary>
    /// An address of a collective resolves when the collective is open to the caller.
    /// </summary>
    public async Task<ResolveResponse> Resolve(Caller caller, IReadOnlyList<Uri> uris, CancellationToken cancellationToken)
    {
        var collectiveIds = uris.Select(_metadataBuilder.GetCollectiveId).ToList();
        var ours = collectiveIds.Any(x => x.HasValue);

        // the shelf is asked only when an address is of this source at all: an address of another source says nothing
        // of what exists here
        var viewer = ours ? await Viewer.Create(caller, _shelfAccountsClient, cancellationToken) : null;
        var found = ours ? await Get(collectiveIds) : new Dictionary<Guid, CollectiveVersion>();

        return new()
        {
            Items = uris
                .Select((uri, i) =>
                {
                    if (collectiveIds[i] is not { } collectiveId)
                        return new ResolvedUri { Uri = uri, State = ResolveState.NotResolved };

                    return viewer!.StateOf(found.GetValueOrDefault(collectiveId)) switch
                    {
                        ItemState.Open => new ResolvedUri { Uri = uri, State = ResolveState.Resolved, Id = ToId(collectiveId) },
                        ItemState.NotOpen => new ResolvedUri { Uri = uri, State = ResolveState.NotResolved },
                        ItemState.Unavailable => new ResolvedUri { Uri = uri, State = ResolveState.Unavailable },
                        _ => throw new ArgumentOutOfRangeException(),
                    };
                })
                .ToList(),
        };
    }

    /// <returns>The picture, or null when this source has none of that name.</returns>
    public async Task<SourceIcon?> GetIcon(string name, CancellationToken cancellationToken)
    {
        if (_options.IconName == null || _options.IconPath == null || name != _options.IconName) return null;

        if (!new FileExtensionContentTypeProvider().TryGetContentType(_options.IconPath, out var contentType))
            throw new($"The content type of the icon {_options.IconPath} is not known.");

        return new(await File.ReadAllBytesAsync(_options.IconPath, cancellationToken), contentType);
    }

    /// <summary>
    /// The collectives of these ids that exist and are not deleted.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, CollectiveVersion>> Get(IEnumerable<Guid?> collectiveIds)
    {
        var ids = collectiveIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, CollectiveVersion>();

        return (await _collectivesApiClient.GetMany(ids)).Versions.ToDictionary(x => x.CollectiveId);
    }

    private Header ToHeader(CollectiveVersion version) => new()
    {
        Id = ToId(version.CollectiveId),
        Source = new()
        {
            Key = _selfOptions.SourceName,
            Title = _selfOptions.SourceName,
            Uri = version.Uri,
            Icon = _options.IconName,
        },
        Title = version.Collective.Title,
        Artists = version.Collective.Authors,
        Kinds = [ContentKind.Sheet],
        Attributes = new()
        {
            Key = version.Collective.Tonality is { } tonality && KeyPattern().IsMatch(tonality) ? tonality : null,
            Bpm = version.Collective.Bpm,
        },
        IsPublic = version.Collective.Visibility == CollectiveVisibility.Public,
    };

    /// <summary>
    /// A key as the contract states it: a note and an optional "m".
    /// </summary>
    [GeneratedRegex("^[A-G][#b]?m?$")]
    private static partial Regex KeyPattern();

    /// <summary>
    /// What is open to one caller, and what its searches find.
    /// </summary>
    private sealed class Viewer
    {
        private readonly bool _isPerson;
        private readonly IReadOnlySet<long>? _telegramUserIds;

        private Viewer(bool isPerson, IReadOnlySet<long>? telegramUserIds)
        {
            _isPerson = isPerson;
            _telegramUserIds = telegramUserIds;
        }

        /// <summary>
        /// The caller is a person whom the shelf could not tell.
        /// </summary>
        public bool IsUnknownPerson => _isPerson && _telegramUserIds == null;

        /// <summary>
        /// For a person, asks the shelf who it is, always and first.
        /// </summary>
        public static async Task<Viewer> Create(Caller caller, ShelfAccountsClient shelfAccountsClient, CancellationToken cancellationToken)
        {
            if (caller.AccountId is not { } accountId) return new(false, null);

            try
            {
                return new(true, await shelfAccountsClient.GetTelegramUserIds(accountId, cancellationToken));
            }
            catch (ShelfUnavailableException)
            {
                return new(true, null);
            }
        }

        /// <param name="version">The collective, or null when there is none of that id.</param>
        public ItemState StateOf(CollectiveVersion? version)
        {
            if (version?.Collective.Visibility is CollectiveVisibility.Public or CollectiveVisibility.Club) return ItemState.Open;
            if (!_isPerson) return ItemState.NotOpen;
            if (_telegramUserIds == null) return ItemState.Unavailable;
            return version != null && _telegramUserIds.Contains(version.CreatedByUserId) ? ItemState.Open : ItemState.NotOpen;
        }

        public bool Finds(CollectiveVersion version)
            => version.Collective.Visibility == CollectiveVisibility.Public
               || (_telegramUserIds?.Contains(version.CreatedByUserId) ?? false);
    }
}
