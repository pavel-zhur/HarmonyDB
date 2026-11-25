namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// Who an operation is made for. A source decides what is open to the caller by its keys alone.
/// </summary>
public record Caller
{
    /// <summary>
    /// The person's account, when the call is made for a signed-in person; null for a guest or a service.
    /// </summary>
    public Guid? AccountId { get; init; }

    /// <summary>
    /// The caller's keys: opaque strings, given by the calling service. Their meaning is the source's.
    /// </summary>
    public required IReadOnlyList<string> Keys { get; init; }
}
