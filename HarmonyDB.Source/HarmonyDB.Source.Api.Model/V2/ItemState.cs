namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// What became of one id asked.
/// </summary>
public enum ItemState
{
    /// <summary>The item is open to the caller: its answer is given.</summary>
    Open,

    /// <summary>The item is not open to the caller, or not known. Nothing else is said of it.</summary>
    NotOpen,

    /// <summary>The source of the item did not answer: nothing is known of it now. Not "not open".</summary>
    Unavailable,
}
