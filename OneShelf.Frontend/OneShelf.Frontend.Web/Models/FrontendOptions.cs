using OneShelf.Common;

namespace OneShelf.Frontend.Web.Models;

public class FrontendOptions
{
    public required string LocalSourceName { get; init; }

    public string LocalSourceNameSafe => LocalSourceName
        ?.Replace("'", string.Empty)
        .Replace("\"", string.Empty)
        .Trim()
        .SelectSingle(x => string.IsNullOrEmpty(x) ? "Local" : x)!;
}