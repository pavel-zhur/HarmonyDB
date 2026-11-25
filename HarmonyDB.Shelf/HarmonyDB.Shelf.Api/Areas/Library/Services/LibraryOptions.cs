namespace HarmonyDB.Shelf.Api.Areas.Library.Services;

/// <summary>
/// The configuration section <c>Library</c>.
/// </summary>
public sealed class LibraryOptions
{
    public const string SectionName = "Library";

    /// <summary>
    /// How many reads of the offline library (the list and the contents) one person may make in a window: a device
    /// pages through a library of a few thousand chords in a few dozen calls.
    /// </summary>
    public int OfflineRateLimitPermits { get; set; } = 120;

    public TimeSpan OfflineRateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);
}
