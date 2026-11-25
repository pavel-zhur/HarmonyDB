using HarmonyDB.Shelf.Model;

namespace HarmonyDB.Shelf.Api.Foundation.Errors;

/// <summary>
/// Thrown by a service to answer the request with an error: the status and a stable code (<see cref="ErrorCodes"/> or
/// an area's own), never human text. The message is for the log only.
/// </summary>
public class ShelfException(int statusCode, string code, IReadOnlyDictionary<string, string>? details = null)
    : Exception($"{statusCode} {code}")
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public IReadOnlyDictionary<string, string>? Details { get; } = details;

    public static ShelfException BadRequest(string code = ErrorCodes.BadRequest, IReadOnlyDictionary<string, string>? details = null)
        => new(StatusCodes.Status400BadRequest, code, details);

    public static ShelfException NotFound(string code = ErrorCodes.NotFound)
        => new(StatusCodes.Status404NotFound, code);

    public static ShelfException Forbidden(string code = ErrorCodes.Forbidden)
        => new(StatusCodes.Status403Forbidden, code);

    public static ShelfException Conflict(string code = ErrorCodes.Conflict, IReadOnlyDictionary<string, string>? details = null)
        => new(StatusCodes.Status409Conflict, code, details);
}
