using System.Globalization;

namespace HarmonyDB.Common.Representations.OneShelf;

/// <summary>
/// The time of a chord, a word, a line or a mark in the rendered HTML: <c>data-time</c>, in seconds from the start,
/// so that a player can show what sounds now and go to the place that is tapped.
/// </summary>
internal static class TimeAttribute
{
    public static string Format(double seconds) => $"data-time='{seconds.ToString(CultureInfo.InvariantCulture)}'";
}
