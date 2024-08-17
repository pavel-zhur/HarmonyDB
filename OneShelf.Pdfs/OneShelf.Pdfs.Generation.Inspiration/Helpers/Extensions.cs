using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace OneShelf.Pdfs.Generation.Inspiration.Helpers;

public static class Extensions
{
    public static string? GetCaption<T>(this T value)
        where T : Enum
    {
        return typeof(T).GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static)
            !.GetCustomAttribute<DisplayAttribute>()
            ?.Name;
    }
}