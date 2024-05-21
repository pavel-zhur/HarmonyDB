using System.Reflection;

namespace OneShelf.Frontend.Web.Models;

public static class HumanTitleExtensions
{
    public static string GetHumanTitle<T>(this T value)
        where T : Enum
    {
        return typeof(T).GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static)
            ?.GetCustomAttribute<HumanTitleAttribute>()?.Title ?? value.ToString();
    }
}