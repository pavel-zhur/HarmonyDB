using OneShelf.Common;
using OneShelf.Common.Songs.FullTextSearch;

namespace OneShelf.Frontend.Api.Model.V3.Databasish;

public class Artist : ISearchableArtist
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<string> Synonyms { get; init; }

    IEnumerable<string> ISearchableArtist.Synonyms => Synonyms;

    public IEnumerable<int> DeepHash()
    {
        return Id
            .Once()
            .Append(Name.GetHashCode())
            .Concat(Synonyms.Select(x => x.GetHashCode()));
    }
}