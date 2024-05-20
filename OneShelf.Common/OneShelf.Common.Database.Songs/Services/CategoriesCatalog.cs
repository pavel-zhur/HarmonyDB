using System.Reflection;
using OneShelf.Common.Database.Songs.Model.Attributes;
using OneShelf.Common.Database.Songs.Model.Enums;

namespace OneShelf.Common.Database.Songs.Services;

public class CategoriesCatalog
{
    public IReadOnlyList<(SongCategory category, string categoryTitle)> All { get; }
        = typeof(SongCategory)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (f, a: f.GetCustomAttribute<SongCategoryTitleAttribute>()))
            .Where(f => f.a != null)
            .Select(f => (Enum.Parse<SongCategory>(f.f.Name), f.a!.Title))
            .ToList();

    public string this[SongCategory category] => All.Single(x => x.category == category).categoryTitle;

    public string? GetPart(SongCategory category, string artist)
    {
        switch (category)
        {
            case SongCategory.Foreign:
                switch (artist[0])
                {
                    case <= 'f':
                        return "1/4. # A B C D E F";
                    case <= 'k':
                        return "2/4. G H I J K";
                    case <= 'p':
                        return "3/4. I J K L M N O P";
                    default:
                        return "4/4. Q R S T U V W X Y Z";
                }

            case SongCategory.Domestic:
                switch (artist[0])
                {
                    case <= 'а':
                        return "1/8. # А";
                    case <= 'в':
                        return "2/8. Б В";
                    case <= 'ж':
                        return "3/8. Г Д Е Ж";
                    case <= 'к':
                        return "4/8. З И К";
                    case <= 'м':
                        return "5/8. Л М";
                    case <= 'п':
                        return "6/8. Н О П";
                    case <= 'с':
                        return "7/8. Р С";
                    default:
                        return "8/8. Т У ... Ю Я";
                }

            case SongCategory.Folclore:
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, null);
        }
    }
}