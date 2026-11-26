using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Collectives.Api.Model.VInternal;
using Version = OneShelf.Collectives.Database.Models.Version;

namespace OneShelf.Collectives.Api.Tools;

internal static class Mapper
{
    public static CollectiveVisibility? ToModelVisibility(this Database.Models.CollectiveVisibility visibility)
        => visibility switch
        {
            Database.Models.CollectiveVisibility.Private => CollectiveVisibility.Private,
            Database.Models.CollectiveVisibility.Club => CollectiveVisibility.Club,
            Database.Models.CollectiveVisibility.Public => CollectiveVisibility.Public,
            Database.Models.CollectiveVisibility.Deleted => null,
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null)
        };

    public static Database.Models.CollectiveVisibility ToDatabaseVisibility(this CollectiveVisibility visibility)
        => visibility switch
        {
            CollectiveVisibility.Private => Database.Models.CollectiveVisibility.Private,
            CollectiveVisibility.Club => Database.Models.CollectiveVisibility.Club,
            CollectiveVisibility.Public => Database.Models.CollectiveVisibility.Public,
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null)
        };

    public static Version ToDatabaseVersion(this Collective collective, int searchTag)
        => new()
        {
            Authors = collective.Authors,
            Bpm = collective.Bpm,
            Contents = collective.Contents,
            CreatedOn = DateTime.Now,
            Title = collective.Title,
            Visibility = collective.Visibility.ToDatabaseVisibility(),
            Description = collective.Description,
            Tonality = collective.Tonality,
            SearchTag = searchTag,
            VersionComment = collective.VersionComment,
        };

    public static OwnCollective ToOwn(this Database.Models.Collective collective, Uri uri, bool withHistory)
        => ToOwn(collective.Id, collective.Versions, uri, withHistory, OldAuthor(collective.CreatedByUserId), false);

    public static OwnCollective ToOwn(this Database.Models.AuthoredCollective collective, Uri uri, bool withHistory)
        => ToOwn(collective.Id, collective.Versions, uri, withHistory, collective.Author, true);

    /// <summary>
    /// The author's name of own chords written in the old shelf, by a Telegram user.
    /// </summary>
    public static string OldAuthor(long telegramUserId) => $"telegram:{telegramUserId}";

    private static OwnCollective ToOwn(Guid id, List<Version> versions, Uri uri, bool withHistory, string author, bool editable)
    {
        var version = versions.Last();
        return new()
        {
            CollectiveId = id,
            Collective = version.ToModel(),
            VersionNumber = versions.Count,
            CreatedOn = versions.First().CreatedOn,
            UpdatedOn = version.CreatedOn,
            SearchTag = version.SearchTag,
            Uri = uri,
            Author = author,
            Editable = editable,
            History = withHistory
                ? versions.Select((x, i) => new OwnCollectiveVersion
                {
                    VersionNumber = i + 1,
                    CreatedOn = x.CreatedOn,
                    Collective = x.ToModel(),
                }).ToList()
                : null,
        };
    }

    private static Collective ToModel(this Version version) => new()
    {
        Title = version.Title,
        Authors = version.Authors,
        Bpm = version.Bpm,
        Contents = version.Contents,
        Description = version.Description,
        Tonality = version.Tonality,
        VersionComment = version.VersionComment,
        Visibility = version.Visibility.ToModelVisibility() ?? throw new("The version is deleted."),
    };

    public static CollectiveVersion ToModelVersion(this Database.Models.Collective collective, Uri uri)
    {
        var version = collective.Versions.Last();
        return new()
        {
            CreatedOn = collective.Versions.First().CreatedOn,
            UpdatedOn = version.CreatedOn,
            CollectiveId = collective.Id,
            CreatedByUserId = collective.CreatedByUserId,
            SearchTag = version.SearchTag,
            VersionNumber = collective.Versions.Count,
            Collective = new()
            {
                Title = version.Title,
                Authors = version.Authors,
                Bpm = version.Bpm,
                Contents = version.Contents,
                Description = version.Description,
                Tonality = version.Tonality,
                VersionComment = version.VersionComment,
                Visibility = version.Visibility.ToModelVisibility() ?? throw new("The collective is deleted."),
            },
            Uri = uri,
        };
    }
}