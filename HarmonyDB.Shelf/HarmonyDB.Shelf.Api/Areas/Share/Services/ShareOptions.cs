namespace HarmonyDB.Shelf.Api.Areas.Share.Services;

/// <summary>
/// The configuration section <c>Share</c>.
/// </summary>
public sealed class ShareOptions
{
    public const string SectionName = "Share";

    /// <summary>
    /// How many times one client address may open links (a link, and the chords it opens, together) in a window.
    /// </summary>
    public int OpenRateLimitPermits { get; set; } = 60;

    public TimeSpan OpenRateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);
}
