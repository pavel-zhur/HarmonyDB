namespace OneShelf.Common.Songs.FullTextSearch;

public interface ISearchableSong
{
    string Title { get; }

    string? AdditionalKeywords { get; }

    int Index { get; }

    IEnumerable<ILike> Likes { get; }
}