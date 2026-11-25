using HarmonyDB.Index.Api.Models;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.Api.Services.V2;

/// <summary>
/// Relays the source contract, version 2, to the configured sources (<see cref="RelayV2Options"/>). An id, and an
/// icon's name, go to the source whose prefix they have; a search and an address go to every source. The caller is passed on as it came, and
/// the answers as the sources gave them: the relay knows no source beyond its configuration.
/// </summary>
public class SourceV2Relay
{
    private readonly ILogger<SourceV2Relay> _logger;
    private readonly IReadOnlyList<(RelayV2Source options, SourceV2Client client)> _sources;

    public SourceV2Relay(IOptions<RelayV2Options> options, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SourceV2Relay>();
        _sources = options.Value.Sources
            .Select(source =>
            {
                var httpClient = httpClientFactory.CreateClient(nameof(SourceV2Relay));
                var client = new SourceV2Client(httpClient, new()
                {
                    Name = source.Name,
                    Endpoint = source.Endpoint,
                    Key = source.Key,
                    Timeout = options.Value.Timeout,
                }, loggerFactory.CreateLogger<SourceV2Client>());
                return (source, client);
            })
            .ToList();
    }

    /// <summary>
    /// Searches every source at once, each within its timeout. The headers come in the order of the sources; a source
    /// that did not answer is in the failures.
    /// </summary>
    public async Task<SearchResponse> Search(Caller caller, string query, CancellationToken cancellationToken)
    {
        var answers = await Task.WhenAll(_sources.Select(x => x.client.Search(caller, query, cancellationToken)));

        var headers = new List<Header>();
        var failures = new List<SearchFailure>();
        for (var i = 0; i < _sources.Count; i++)
        {
            var answer = answers[i];
            var stray = answer.Headers.FirstOrDefault(h => Route(h.Id) != i);
            if (stray != null)
            {
                // the relay could not bring an id of this answer back to its source
                _logger.LogError("The source {source} found the id {id}, which is not of its prefixes.", _sources[i].options.Name, stray.Id);
                failures.Add(new()
                {
                    Source = _sources[i].options.Name,
                    Reason = SearchFailureReason.Unavailable,
                });
                continue;
            }

            headers.AddRange(answer.Headers);
            failures.AddRange(answer.Failures);
        }

        return new()
        {
            Headers = headers,
            Failures = failures,
        };
    }

    /// <summary>
    /// Asks each source for its ids at once; an id of no source is not open.
    /// </summary>
    public async Task<GetHeadersResponse> GetHeaders(Caller caller, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var answers = new HeaderAnswer[ids.Count];
        var bySource = new Dictionary<int, List<int>>();
        for (var i = 0; i < ids.Count; i++)
        {
            var source = Route(ids[i]);
            if (source == null)
            {
                answers[i] = new()
                {
                    Id = ids[i],
                    State = ItemState.NotOpen,
                };
            }
            else
            {
                (bySource.TryGetValue(source.Value, out var positions) ? positions : bySource[source.Value] = []).Add(i);
            }
        }

        await Task.WhenAll(bySource.Select(async x =>
        {
            var response = await _sources[x.Key].client.GetHeaders(caller, x.Value.Select(i => ids[i]).ToList(), cancellationToken);
            for (var k = 0; k < x.Value.Count; k++)
            {
                answers[x.Value[k]] = response.Headers[k];
            }
        }));

        return new()
        {
            Headers = answers,
        };
    }

    public async Task<GetContentResponse> GetContent(Caller caller, string id, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var source = Route(id);
        if (source == null)
        {
            return new()
            {
                Id = id,
                State = ItemState.NotOpen,
            };
        }

        return await _sources[source.Value].client.GetContent(caller, id, kinds, cancellationToken);
    }

    /// <summary>
    /// Asks every source for every address at once. An address is resolved by the first source (in the configured
    /// order) that resolves it; otherwise it is unavailable when a source did not answer for it, and not resolved
    /// when none knows it.
    /// </summary>
    public async Task<ResolveResponse> Resolve(Caller caller, IReadOnlyList<Uri> uris, CancellationToken cancellationToken)
    {
        var answers = await Task.WhenAll(_sources.Select(x => x.client.Resolve(caller, uris, cancellationToken)));

        return new()
        {
            Items = uris.Select((uri, position) =>
            {
                var unavailable = false;
                for (var i = 0; i < _sources.Count; i++)
                {
                    var item = answers[i].Items[position];
                    switch (item.State)
                    {
                        case ResolveState.Resolved when Route(item.Id!) == i:
                            return item;
                        case ResolveState.Resolved:
                            _logger.LogError("The source {source} resolved an address to the id {id}, which is not of its prefixes.", _sources[i].options.Name, item.Id);
                            unavailable = true;
                            break;
                        case ResolveState.Unavailable:
                            unavailable = true;
                            break;
                    }
                }

                return new ResolvedUri
                {
                    Uri = uri,
                    State = unavailable ? ResolveState.Unavailable : ResolveState.NotResolved,
                };
            }).ToList(),
        };
    }

    /// <summary>
    /// The picture of this name, from the source it routes to: an icon's name begins with one of its source's id
    /// prefixes, as an id does.
    /// </summary>
    /// <returns>The picture, or null when the name is of no source, or its source has no such picture.</returns>
    /// <exception cref="SourceUnavailableException">Its source did not answer.</exception>
    public async Task<SourceIcon?> GetIcon(string name, CancellationToken cancellationToken)
        => Route(name) is { } source
            ? await _sources[source].client.GetIcon(name, cancellationToken)
            : null;

    private int? Route(string id)
    {
        for (var i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].options.IdPrefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal))) return i;
        }

        return null;
    }
}
