using OneShelf.Frontend.Api.Model.V3.Databasish.Interfaces;

namespace OneShelf.Frontend.Api.Model.V3.Databasish;

public class Collection : ICollection
{
    public required List<Song> Songs { get; init; }
    IReadOnlyList<ISong> ICollection.Songs => Songs;

    public required List<User> Users { get; init; }
    IReadOnlyList<User> ICollection.Users => Users;

    public required List<Artist> Artists { get; init; }
    IReadOnlyList<Artist> ICollection.Artists => Artists;

    public required List<LikeCategory> LikeCategories { get; init; }
    IReadOnlyList<LikeCategory> ICollection.LikeCategories => LikeCategories;

    public int? Etag { get; set; }

    public IEnumerable<int> DeepHash() =>
        Songs.SelectMany(x => x.DeepHash())
            .Concat(Users.SelectMany(x => x.DeepHash()))
            .Concat(Artists.SelectMany(x => x.DeepHash()))
            .Concat(LikeCategories.SelectMany(x => x.DeepHash()))
            .Append(Songs.Count)
            .Append(Users.Count)
            .Append(Artists.Count)
            .Append(LikeCategories.Count);

    public Collection Clone() =>
        new()
        {
            Artists = Artists.ToList(),
            Songs = Songs.ToList(),
            Users = Users.ToList(),
            Etag = Etag,
            LikeCategories = LikeCategories.ToList(),
        };
}