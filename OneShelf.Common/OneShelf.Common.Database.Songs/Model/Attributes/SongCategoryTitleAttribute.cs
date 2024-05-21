namespace OneShelf.Common.Database.Songs.Model.Attributes;

public class SongCategoryTitleAttribute : Attribute
{
    public SongCategoryTitleAttribute(string title)
    {
        Title = title;
    }

    public string Title { get; }
}