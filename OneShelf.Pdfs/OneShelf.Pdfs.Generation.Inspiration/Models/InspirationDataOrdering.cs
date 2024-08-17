using System.ComponentModel.DataAnnotations;

namespace OneShelf.Pdfs.Generation.Inspiration.Models;

public enum InspirationDataOrdering
{
    [Display(Name = "Старые -> новые")]
    ByIndexAsc,

    [Display(Name = "Новые -> старые")]
    ByIndexDesc,

    [Display(Name = "По исполнителям (поисковик)")]
    ByArtistWithSynonyms,

    [Display(Name = "По исполнителям")]
    ByArtist,

    [Display(Name = "По названию")]
    ByTitle,
}