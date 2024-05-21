namespace OneShelf.Frontend.Web.Models;

public class HumanTitleAttribute : Attribute
{
    public string Title { get; }

    public HumanTitleAttribute(string title)
    {
        Title = title;
    }
}