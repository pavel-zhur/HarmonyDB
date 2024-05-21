using OneShelf.Frontend.Api.Model.V3.Databasish;

namespace OneShelf.Frontend.Api.Model.V3.Databasish.Interfaces;

public interface ICollection
{
    IReadOnlyList<ISong> Songs { get; }
    IReadOnlyList<User> Users { get; }
    IReadOnlyList<Artist> Artists { get; }
    IReadOnlyList<LikeCategory> LikeCategories { get; }
    int? Etag { get; }
    IEnumerable<int> DeepHash();
    Collection Clone();
}