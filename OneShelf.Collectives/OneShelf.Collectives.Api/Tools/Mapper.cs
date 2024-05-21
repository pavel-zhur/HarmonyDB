using OneShelf.Collectives.Api.Model.V2.Sub;
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