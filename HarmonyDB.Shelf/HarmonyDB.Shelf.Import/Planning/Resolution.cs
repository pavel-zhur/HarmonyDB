namespace HarmonyDB.Shelf.Import.Planning;

/// <summary>
/// The old addresses (and the ids visits were opened by) as the relay resolved them: the source's id, or why not.
/// </summary>
public sealed class Resolution(IReadOnlyDictionary<string, ResolvedAddress> addresses, IReadOnlyDictionary<string, ResolvedAddress> ids)
{
    public static Resolution Empty { get; } = new(new Dictionary<string, ResolvedAddress>(), new Dictionary<string, ResolvedAddress>());

    public IReadOnlyDictionary<string, ResolvedAddress> Addresses => addresses;

    public IReadOnlyDictionary<string, ResolvedAddress> Ids => ids;

    /// <summary>
    /// The source's id of the chords at the address; null when not resolved (yet).
    /// </summary>
    public string? IdOf(string uri) => addresses.GetValueOrDefault(uri)?.SourceId;

    /// <summary>
    /// The source's id a visit's id stands for, when the relay knows it as open; null otherwise.
    /// </summary>
    public string? ConfirmedId(string externalId) => ids.GetValueOrDefault(externalId)?.SourceId;
}

/// <param name="SourceId">The id; null when not resolved.</param>
/// <param name="State">The relay's answer ("resolved", "notResolved", "unavailable", "open", "notOpen" …).</param>
public sealed record ResolvedAddress(string? SourceId, string State);
