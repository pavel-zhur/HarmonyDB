namespace OneShelf.Common.Songs.FullTextSearch;

public interface ISearchableArtist
{
    string Name { get; }

    IEnumerable<string> Synonyms { get; }
}