using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;

namespace HarmonyDB.Shelf.Import.Writing;

/// <summary>
/// The illustrations service (section <see cref="SectionName"/>), where the illustrations of the old shelf are kept under
/// the old addresses: the import gives it the chords' ids those addresses resolved to, as aliases, so the shelf shows an
/// item's old illustrations by its id. Called as an internal client with its key.
/// </summary>
public sealed class IllustrationAliasesOptions
{
    public const string SectionName = "Illustrations";

    /// <summary>
    /// The base address of the service's operations, ending with a slash.
    /// </summary>
    public Uri? Endpoint { get; set; }

    public string Key { get; set; } = string.Empty;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}

/// <param name="Added">The ids aliased now to an address with illustrations.</param>
/// <param name="Unchanged">The ids aliased so already.</param>
/// <param name="NotIllustrated">The ids none of whose addresses has illustrations.</param>
/// <param name="OwnAlready">The ids with illustrations of their own already (not aliased).</param>
/// <param name="IllustratedWithoutAlias">The illustrated addresses no id stands for yet (not resolved).</param>
public sealed record IllustrationAliasesResult(int Added, int Unchanged, int NotIllustrated, int OwnAlready, int IllustratedWithoutAlias);

public sealed class IllustrationAliases(HttpClient http, IllustrationAliasesOptions options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Puts the pairs (id, old address) at once: the service keeps for each id its illustrated address, and putting the
    /// same again changes nothing.
    /// </summary>
    public async Task<IllustrationAliasesResult> PutAsync(IReadOnlyCollection<(string id, string address)> pairs, CancellationToken cancellationToken)
    {
        if (options.Endpoint is not { IsAbsoluteUri: true } || string.IsNullOrEmpty(options.Key))
            throw new InvalidOperationException($"{IllustrationAliasesOptions.SectionName}:Endpoint and :Key are not set.");

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(options.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.Endpoint, "ShelfAliases"))
        {
            Content = JsonContent.Create(new { aliases = pairs.Select(x => new { key = x.id, url = x.address }) }, options: Json),
        };
        request.Headers.Add(InternalKeyDefaults.HeaderName, options.Key);
        using var response = await http.SendAsync(request, limit.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IllustrationAliasesResult>(Json, limit.Token)
               ?? throw new InvalidOperationException("The illustrations service gave an empty answer.");
    }
}
