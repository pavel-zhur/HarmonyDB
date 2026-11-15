namespace OneShelf.Common.Api;

/// <summary>
/// Who may call a function.
/// </summary>
public enum AuthorizationLevel
{
    /// <summary>Anyone.</summary>
    Anonymous,

    /// <summary>A caller presenting one of the module's function keys, in the "code" query parameter or the "x-functions-key" header.</summary>
    Function,
}
