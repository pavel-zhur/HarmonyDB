using System.Text.RegularExpressions;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using OneShelf.Collectives.Api.Client;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Sources.Self.Api.Models;

namespace OneShelf.Sources.Self.Api.Services.V2;

/// <summary>
/// The own chords (the collectives) as a source of the contract, version 2.
/// <list type="bullet">
/// <item>The id of a collective is <c>mj-{its id}</c>: it stays the same through the collective's versions.</item>
/// <item>Its author is the opaque name the store keeps; it is the caller's when any of the caller's subjects is it. The
/// source compares names and asks no one what one means.</item>
/// <item>A public collective is found by every search and open to everyone. A club one is found by its author's
/// searches, and open to everyone who holds its id. A private one is found by its author's searches, and open to its
/// author and to whoever holds its current address (<see cref="SelfAddresses"/>): its id, its code and its version are
/// the collective's. A new version takes the private links handed out before it back. A deleted collective is not
/// open. The caller's keys open nothing here.</item>
/// <item>An address resolves when the collective is open to the caller with it: its current address for everyone, an
/// older address of a public or club collective for everyone, an older address of a private one for its author.</item>
/// <item>An item's address in its header is its current one, by this source's own template.</item>
/// <item>The icon's name begins with <see cref="IdPrefix"/>: the index routes it to this source as it routes an id.</item>
/// </list>
/// </summary>
public partial class SelfV2Source
{
    public const string IdPrefix = "mj-";

    private readonly CollectivesApiClient _collectivesApiClient;
    private readonly StructureParser _structureParser;
    private readonly SelfAddresses _addresses;
    private readonly SelfApiOptions _selfOptions;
    private readonly SelfV2Options _options;

    public SelfV2Source(CollectivesApiClient collectivesApiClient, StructureParser structureParser, SelfAddresses addresses, IOptions<SelfApiOptions> selfOptions, IOptions<SelfV2Options> options)
    {
        _collectivesApiClient = collectivesApiClient;
        _structureParser = structureParser;
        _addresses = addresses;
        _selfOptions = selfOptions.Value;
        _options = options.Value;
    }

    public static string ToId(Guid collectiveId) => $"{IdPrefix}{collectiveId:D}";

    public static Guid? FromId(string id)
        => id.StartsWith(IdPrefix, StringComparison.Ordinal) && Guid.TryParseExact(id[IdPrefix.Length..], "D", out var collectiveId) && ToId(collectiveId) == id
            ? collectiveId
            : null;

    /// <summary>
    /// The source's one label.
    /// </summary>
    public SourceLabel Label => new()
    {
        Key = _selfOptions.SourceName,
        Title = _selfOptions.SourceName,
        Icon = _options.IconName,
    };

    /// <summary>
    /// The collectives the caller finds whose title or authors contain every word of the query, by title.
    /// </summary>
    public async Task<SearchResponse> Search(Caller caller, string query, CancellationToken cancellationToken)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var headers = (await _collectivesApiClient.Items(null)).Items
            .Where(version => Finds(caller, version))
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
            Failures = [],
        };
    }

    public async Task<GetHeadersResponse> GetHeaders(Caller caller, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var found = await Get(ids.Select(FromId));

        return new()
        {
            Headers = ids
                .Select(id =>
                {
                    var version = FromId(id) is { } collectiveId ? found.GetValueOrDefault(collectiveId) : null;
                    return IsOpen(caller, version, null)
                        ? new HeaderAnswer { Id = id, State = ItemState.Open, Header = ToHeader(version!) }
                        : new HeaderAnswer { Id = id, State = ItemState.NotOpen };
                })
                .ToList(),
        };
    }

    public async Task<GetContentResponse> GetContent(Caller caller, string id, IReadOnlyList<ContentKind> kinds, Uri? address, CancellationToken cancellationToken)
    {
        var collectiveId = FromId(id);
        var version = collectiveId == null ? null : (await Get([collectiveId])).GetValueOrDefault(collectiveId.Value);
        return ContentOf(caller, id, version, kinds, address);
    }

    /// <summary>
    /// The content of several items, read from the store in one call.
    /// </summary>
    public async Task<GetContentsResponse> GetContents(Caller caller, IReadOnlyList<string> ids, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var found = await Get(ids.Select(FromId));
        return new()
        {
            Items = ids.Select(id => ContentOf(caller, id, FromId(id) is { } collectiveId ? found.GetValueOrDefault(collectiveId) : null, kinds, null)).ToList(),
        };
    }

    private GetContentResponse ContentOf(Caller caller, string id, OwnCollective? version, IReadOnlyList<ContentKind> kinds, Uri? address)
    {
        if (!IsOpen(caller, version, address))
        {
            return new()
            {
                Id = id,
                State = ItemState.NotOpen,
            };
        }

        return new()
        {
            Id = id,
            State = ItemState.Open,
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
    /// An address of a collective resolves when the collective is open to the caller with it.
    /// </summary>
    public async Task<ResolveResponse> Resolve(Caller caller, IReadOnlyList<Uri> uris, CancellationToken cancellationToken)
    {
        var parts = uris.Select(_addresses.Read).ToList();
        var found = parts.Any(x => x != null) ? await Get(parts.Select(x => x?.CollectiveId)) : new Dictionary<Guid, OwnCollective>();

        return new()
        {
            Items = uris
                .Select((uri, i) => parts[i] is { } address && found.GetValueOrDefault(address.CollectiveId) is { } version && IsOpen(caller, version, uri)
                    ? new ResolvedUri { Uri = uri, State = ResolveState.Resolved, Id = ToId(address.CollectiveId) }
                    : new ResolvedUri { Uri = uri, State = ResolveState.NotResolved })
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
    /// Whether the collective is open to the caller, who came by the address given or by its id.
    /// </summary>
    /// <param name="version">The collective, or null when there is none of that id.</param>
    private bool IsOpen(Caller caller, OwnCollective? version, Uri? address)
        => version?.Collective.Visibility switch
        {
            null => false,
            CollectiveVisibility.Public or CollectiveVisibility.Club => true,
            _ => IsAuthor(caller, version) || IsCurrent(version, address),
        };

    private bool Finds(Caller caller, OwnCollective version)
        => version.Collective.Visibility == CollectiveVisibility.Public || IsAuthor(caller, version);

    private static bool IsAuthor(Caller caller, OwnCollective version) => caller.Subjects.Contains(version.Author, StringComparer.Ordinal);

    /// <summary>
    /// Whether the address is the collective's current one: its id, its code and its version are the collective's.
    /// </summary>
    private bool IsCurrent(OwnCollective version, Uri? address)
        => address != null && _addresses.Read(address) is { } parts
                           && parts == new SelfAddresses.Parts(version.CollectiveId, version.SearchTag, version.VersionNumber);

    /// <summary>
    /// The collectives of these ids that exist and are not deleted.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, OwnCollective>> Get(IEnumerable<Guid?> collectiveIds)
    {
        var ids = collectiveIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, OwnCollective>();

        return (await _collectivesApiClient.Items(ids)).Items.ToDictionary(x => x.CollectiveId);
    }

    private Header ToHeader(OwnCollective version) => new()
    {
        Id = ToId(version.CollectiveId),
        Source = Label,
        Address = _addresses.Make(version.CollectiveId, version.SearchTag, version.VersionNumber),
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
}
