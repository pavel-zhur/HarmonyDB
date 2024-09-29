using Microsoft.Extensions.Options;
using OneShelf.Videos.BusinessLogic.Models;

namespace OneShelf.Videos.BusinessLogic.Services;

public class Paths(IOptions<VideosOptions> options)
{
    public string GetLiveDownloadedPath(string fileName) => Path.Combine(options.Value.BasePath, "_instant", fileName);
}