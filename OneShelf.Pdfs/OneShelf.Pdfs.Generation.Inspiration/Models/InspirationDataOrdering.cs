namespace OneShelf.Pdfs.Generation.Inspiration.Models;

public enum InspirationDataOrdering
{
    [StrictChoiceCaption("Старые -> новые")]
    ByIndexAsc,

    [StrictChoiceCaption("Новые -> старые")]
    ByIndexDesc,

    [StrictChoiceCaption("По исполнителям (поисковик)")]
    ByArtistWithSynonyms,

    [StrictChoiceCaption("По исполнителям")]
    ByArtist,

    [StrictChoiceCaption("По названию")]
    ByTitle,
}