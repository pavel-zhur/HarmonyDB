using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.PublicPage.Services;

/// <summary>
/// The previews kept, apart from the app's other kept answers: at most <see cref="PublicPageOptions.KeepLimit"/> at
/// once (the oldest go first), whatever addresses are asked.
/// </summary>
public sealed class PreviewCache(IOptions<PublicPageOptions> options) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = options.Value.KeepLimit });

    public bool TryGet(string path, out string? html) => _cache.TryGetValue(path, out html);

    public void Set(string path, string? html, TimeSpan keep)
        => _cache.Set(path, html, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = keep });

    public void Dispose() => _cache.Dispose();
}
