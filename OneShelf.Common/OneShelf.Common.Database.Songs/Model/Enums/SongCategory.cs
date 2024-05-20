using OneShelf.Common.Database.Songs.Model.Attributes;

namespace OneShelf.Common.Database.Songs.Model.Enums;

public enum SongCategory
{
    [SongCategoryTitle("Зарубежное")]
    Foreign,

    [SongCategoryTitle("Русское")]
    Domestic,

    [SongCategoryTitle("Кино народное и все такое")]
    Folclore
}