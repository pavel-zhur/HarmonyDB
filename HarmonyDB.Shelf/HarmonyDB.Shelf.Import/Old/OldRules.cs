using System.Text.RegularExpressions;

namespace HarmonyDB.Shelf.Import.Old;

/// <summary>
/// The old shelf's own rules, as its code had them, used to read its data as its people saw it.
/// </summary>
public static partial class OldRules
{
    /// <summary>
    /// The songs a tenant got copied from the templates have their template's index, below this; the tenant's own songs
    /// come after it (the copying set the tenant's latest index to it).
    /// </summary>
    public const int TemplateIndexLimit = 1000;

    /// <summary>
    /// A user is a person, with a Telegram user id, when the id is positive; the others are the old shelf's service
    /// and test users.
    /// </summary>
    public static bool IsPerson(long userId) => userId > 0;

    /// <summary>
    /// The songs a person's library showed: live and draft ones (archived and postponed ones were not shown).
    /// </summary>
    public static bool IsShown(OldSong song) => song.Status is OldSongStatus.Live or OldSongStatus.Draft;

    /// <summary>
    /// The template set: the default-template versions of the template tenant's live songs with a template index.
    /// </summary>
    public static bool IsTemplate(OldVersion version, OldSong song, int templateTenantId)
        => version.IsDefaultTemplate && song.TenantId == templateTenantId && song.Index < TemplateIndexLimit && song.Status == OldSongStatus.Live;

    /// <summary>
    /// Whether a song of a personal tenant is a copy of a template song: the copying gave it its template's index and
    /// moved the tenant's index to <see cref="TemplateIndexLimit"/>; a tenant whose copying failed numbered its own songs
    /// from 1.
    /// </summary>
    public static bool IsTemplateCopy(OldSong song, OldTenant tenant)
        => song.Index < TemplateIndexLimit && tenant.LatestUsedIndex >= TemplateIndexLimit;

    /// <summary>
    /// The person the old shelf made a personal tenant for, by its description ("Personal &lt;user id&gt;").
    /// </summary>
    public static long? PersonalTenantOf(OldTenant tenant)
        => tenant.PrivateDescription != null && PersonalDescription().Match(tenant.PrivateDescription) is { Success: true } match
            ? long.Parse(match.Groups[1].Value)
            : null;

    /// <summary>
    /// The versions of a song a person saw: all but others' private own chords.
    /// </summary>
    public static bool IsVisible(OldVersion version, long viewer)
        => version.CollectiveType is null or OldCollectiveType.Public || version.UserId == viewer;

    /// <summary>
    /// The version a song opened at for a person (the old frontend's rule): the person's own chords first, then others'
    /// own chords, then the rest; among them the one the person liked the most (likes outside lists); then the oldest;
    /// then the first added.
    /// </summary>
    /// <param name="visibleVersions">The song's versions the person sees.</param>
    /// <param name="songLikes">The likes of the song.</param>
    public static OldVersion? DefaultVersion(long viewer, IReadOnlyCollection<OldVersion> visibleVersions, IReadOnlyCollection<OldLike> songLikes)
        => visibleVersions
            .OrderBy(x => x.CollectiveId != null ? x.UserId == viewer ? 1 : 2 : 3)
            .ThenByDescending(x => songLikes.Where(l => l.VersionId == x.Id && l.UserId == viewer && l.ListId == null).Select(l => l.Level).DefaultIfEmpty(0).Max())
            .ThenBy(x => x.CreatedOn)
            .ThenBy(x => x.Id)
            .FirstOrDefault();

    /// <summary>
    /// The key a person saw chords in (the old frontend's rule): their first like of the version outside lists, else
    /// their first like of it in a list; its transposition (none: 0). Null when they had no like of the version.
    /// </summary>
    /// <param name="likes">The person's likes of the versions that are these chords.</param>
    public static int? DefaultTransposition(IEnumerable<OldLike> likes)
        => DefaultTranspositionLike(likes) is { } like ? like.Transpose ?? 0 : null;

    /// <summary>
    /// The like whose transposition is <see cref="DefaultTransposition"/>.
    /// </summary>
    public static OldLike? DefaultTranspositionLike(IEnumerable<OldLike> likes)
        => likes
            .Where(x => x.VersionId != null)
            .OrderBy(x => x.ListId == null ? 1 : 2)
            .ThenBy(x => x.Id)
            .FirstOrDefault();

    /// <summary>
    /// The recommended transposition kept in a version's published settings ("transpose=-2, two-columns"); null when
    /// there is none.
    /// </summary>
    public static int? PublishedTransposition(string? publishedSettings)
        => publishedSettings != null && PublishedTranspose().Match(publishedSettings) is { Success: true } match
            ? int.Parse(match.Groups[1].Value)
            : null;

    /// <summary>
    /// Whether a text looks like UTF-8 read as single bytes: it has control characters that no written text has (bytes
    /// of UTF-8 sequences, read one by one); or all its characters are single bytes, some above ASCII, and those bytes are
    /// valid UTF-8 of other text.
    /// </summary>
    public static bool LooksMisdecoded(string text)
    {
        if (text.Any(x => x is >= '\u0080' and <= '\u009F'))
            return true;

        if (text.Any(x => x > 0xFF) || !text.Any(x => x > 0x7F))
            return false;

        try
        {
            var decoded = new System.Text.UTF8Encoding(false, true).GetString(System.Text.Encoding.Latin1.GetBytes(text));
            return decoded != text;
        }
        catch (System.Text.DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// A time of the old database; the earliest one stands for "unknown" (rows from before the column existed).
    /// </summary>
    public static DateTimeOffset? Time(DateTime value)
        => value == DateTime.MinValue ? null : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    [GeneratedRegex(@"^Personal (-?\d+)$")]
    private static partial Regex PersonalDescription();

    [GeneratedRegex(@"(?:^|,)\s*transpose=(-?\d+)\s*(?:,|$)")]
    private static partial Regex PublishedTranspose();
}
