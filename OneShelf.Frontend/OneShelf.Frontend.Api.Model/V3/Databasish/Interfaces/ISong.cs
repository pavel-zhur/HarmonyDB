using OneShelf.Common.Songs.FullTextSearch;
using OneShelf.Frontend.Api.Model.V3.Databasish;

namespace OneShelf.Frontend.Api.Model.V3.Databasish.Interfaces;

public interface ISong : ISearchableSong
{
    int Id { get; }
    IReadOnlyList<int> Artists { get; }
    long CreatedByUserId { get; }
    DateTime CreatedOn { get; }
    IReadOnlyList<Version> Versions { get; }
    new IReadOnlyList<Like> Likes { get; }
    IReadOnlyDictionary<long, string> Comments { get; }
    IEnumerable<int> DeepHash();
    Song Clone();
}