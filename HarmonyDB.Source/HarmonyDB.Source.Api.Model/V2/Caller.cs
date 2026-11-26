namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// Who an operation is made for. A source decides what is open to the caller by its subjects and keys, and by the
/// address the caller came by where an operation takes one; it asks no one what they mean.
/// </summary>
public record Caller
{
    /// <summary>
    /// The person, as the calling service names them: opaque strings, each once. The first is the person's name now;
    /// the others are other names of the same person. What the person writes is authored by the first; what is open to
    /// its author is open when any of them is the author. Empty for a guest or a service.
    /// </summary>
    public required IReadOnlyList<string> Subjects { get; init; }

    /// <summary>
    /// The caller's keys: opaque strings, given by the calling service. Their meaning is the source's. A key is never a
    /// subject.
    /// </summary>
    public required IReadOnlyList<string> Keys { get; init; }

    /// <summary>
    /// Whether the caller is one the contract takes: its lists are there, with nothing empty in them, each subject once.
    /// </summary>
    public static bool IsValid(Caller? caller)
        => caller is { Subjects: not null, Keys: not null }
           && caller.Subjects.All(x => !string.IsNullOrEmpty(x)) && caller.Subjects.Distinct(StringComparer.Ordinal).Count() == caller.Subjects.Count
           && caller.Keys.All(x => x != null);
}