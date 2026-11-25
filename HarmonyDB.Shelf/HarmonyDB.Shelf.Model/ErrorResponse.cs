namespace HarmonyDB.Shelf.Model;

/// <summary>
/// The body of every error answer of the API. The backend gives no human text: the frontend words the code in the
/// person's language.
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// A stable code (<see cref="ErrorCodes"/>, or an area's own codes), snake case.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Values the frontend may put into its words (a limit, a name), by name; absent when there are none.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Details { get; init; }
}
