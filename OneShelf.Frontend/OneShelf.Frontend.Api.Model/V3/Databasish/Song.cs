using OneShelf.Common;
using OneShelf.Common.Songs;
using OneShelf.Common.Songs.FullTextSearch;
using OneShelf.Frontend.Api.Model.V3.Databasish.Interfaces;

namespace OneShelf.Frontend.Api.Model.V3.Databasish;

public class Song : ISearchableSong, ISong
{
    public required int Id { get; init; }

    public required int Index { get; init; }

    public required string Title { get; init; }

    public float? TemplateRating { get; init; }

    public required List<int> Artists { get; init; }
    IReadOnlyList<int> ISong.Artists => Artists;

    public required string? AdditionalKeywords { get; init; }

    public required long CreatedByUserId { get; init; }

    public required DateTime CreatedOn { get; init; }

    public required List<Version> Versions { get; init; }
    IReadOnlyList<Version> ISong.Versions => Versions;

    public required List<Like> Likes { get; init; }
    IReadOnlyList<Like> ISong.Likes => Likes;

    public required Dictionary<long, string> Comments { get; init; }
    IReadOnlyDictionary<long, string> ISong.Comments => Comments;

    IEnumerable<ILike> ISearchableSong.Likes => Likes;

    public IEnumerable<int> DeepHash() =>
        Id
            .Once()
            .Append(Index)
            .Append(TemplateRating?.GetHashCode() ?? 0)
            .Append(Title.GetHashCode())
            .Concat(Artists)
            .Append(AdditionalKeywords?.GetHashCode() ?? 0)
            .Append(CreatedByUserId.GetHashCode())
            .Append(CreatedOn.GetHashCode())
            .Concat(Versions.SelectMany(x => x.DeepHash()))
            .Concat(Likes.SelectMany(x => x.DeepHash()))
            .Concat(Comments.Select(p => p.Key.GetHashCode() ^ p.Value.GetHashCode()));

    public Song Clone() =>
        new()
        {
            Id = Id,
            Versions = Versions.ToList(),
            Likes = Likes.ToList(),
            Title = Title,
            Artists = Artists.ToList(),
            AdditionalKeywords = AdditionalKeywords,
            Comments = Comments.ToDictionary(x => x.Key, x => x.Value),
            CreatedByUserId = CreatedByUserId,
            CreatedOn = CreatedOn,
            Index = Index,
            TemplateRating = TemplateRating,
        };
}