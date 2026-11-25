namespace HarmonyDB.Shelf.Model;

/// <summary>
/// The error codes shared by all areas. An area keeps its own codes in its own folder of this project.
/// </summary>
public static class ErrorCodes
{
    /// <summary>400: the request is malformed or a value in it is not allowed.</summary>
    public const string BadRequest = "bad_request";

    /// <summary>401: the action needs a signed-in person; the frontend offers sign-in and repeats the action.</summary>
    public const string SignInRequired = "sign_in_required";

    /// <summary>403: the person (or the client acting for them) does not have the permission the action needs.</summary>
    public const string PermissionDenied = "permission_denied";

    /// <summary>403: the request is not allowed for another reason (the thing is not the person's to change).</summary>
    public const string Forbidden = "forbidden";

    /// <summary>404: no such thing, or not open to the caller.</summary>
    public const string NotFound = "not_found";

    /// <summary>409: the change conflicts with the current state.</summary>
    public const string Conflict = "conflict";

    /// <summary>429: too many requests; try later.</summary>
    public const string TooManyRequests = "too_many_requests";

    /// <summary>500: something broke on the server.</summary>
    public const string InternalError = "internal_error";

    /// <summary>503: a service the action needs does not answer.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>The thing is marked "soon" in the app and does not work yet.</summary>
    public const string NotYet = "not_yet";
}
