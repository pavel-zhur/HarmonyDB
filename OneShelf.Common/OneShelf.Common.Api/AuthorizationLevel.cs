namespace OneShelf.Common.Api;

/// <summary>
/// Who may call a function.
/// </summary>
public enum AuthorizationLevel
{
    /// <summary>Anyone.</summary>
    Anonymous,

    /// <summary>
    /// A caller presenting one of the module's function keys, in the "code" query parameter or the "x-functions-key"
    /// header; or, when the function names an <see cref="FunctionAttribute.InternalScope"/>, an internal client with that
    /// scope.
    /// </summary>
    Function,

    /// <summary>
    /// Only an internal client, by its key, with the function's <see cref="FunctionAttribute.InternalScope"/>.
    /// </summary>
    Internal,
}
