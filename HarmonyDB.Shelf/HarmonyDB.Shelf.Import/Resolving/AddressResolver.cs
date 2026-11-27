using HarmonyDB.Shelf.Import.Planning;
using HarmonyDB.Source.Api.Client.V2;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;

namespace HarmonyDB.Shelf.Import.Resolving;

/// <summary>
/// Resolves the old addresses to the sources' ids at the index's relay, as the import's internal client, with the
/// configured caller keys.
/// </summary>
public sealed class AddressResolver(SourceV2Client relay, RelayOptions options)
{
    public const string InvalidAddress = "invalidAddress";

    /// <param name="subjects">The names of the person the call is made for (an author's own chords open to them); none for nobody.</param>
    public async Task<Dictionary<string, ResolvedAddress>> ResolveAsync(IReadOnlyCollection<string> uris, IReadOnlyList<string> subjects, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ResolvedAddress>(StringComparer.Ordinal);
        var valid = new List<(string text, Uri uri)>();
        foreach (var text in uris)
        {
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
                valid.Add((text, uri));
            else
                result[text] = new(null, InvalidAddress);
        }

        foreach (var batch in valid.Chunk(options.BatchSize))
        {
            var response = await relay.Resolve(Caller(subjects), batch.Select(x => x.uri).ToList(), cancellationToken);
            await RequireAnsweredAsync(response.Items.Select(x => x.State == ResolveState.Unavailable), cancellationToken);
            for (var i = 0; i < batch.Length; i++)
            {
                var item = response.Items[i];
                result[batch[i].text] = new(item.State == ResolveState.Resolved ? item.Id : null, Name(item.State));
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves one address with the second pass's keys, under which a source may go out for it. A relay that does not
    /// answer stops the pass (<see cref="SourceUnavailableException"/>).
    /// </summary>
    public async Task<ResolvedAddress> ResolveLiveAsync(string address, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            return new(null, InvalidAddress);

        var item = (await relay.Resolve(new() { Subjects = [], Keys = options.SecondPassCallerKeys }, [uri], cancellationToken)).Items.Single();
        if (item.State == ResolveState.Unavailable)
            await relay.Ping(cancellationToken);
        return new(item.State == ResolveState.Resolved ? item.Id : null, Name(item.State));
    }

    /// <summary>
    /// A whole batch unavailable may be the relay itself not answering: then (its ping fails) the run stops before it
    /// writes anything on such answers, rather than taking every address as unresolved. A source behind it that does not
    /// answer is an answer: its addresses wait for a later run.
    /// </summary>
    private async Task RequireAnsweredAsync(IEnumerable<bool> unavailable, CancellationToken cancellationToken)
    {
        if (unavailable.All(x => x))
            await relay.Ping(cancellationToken);
    }

    private Caller Caller(IReadOnlyList<string> subjects) => new() { Subjects = subjects, Keys = options.CallerKeys };

    private static string Name<T>(T state) where T : Enum => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(state.ToString());
}
