using CasCap.Services;
using Microsoft.Extensions.Logging;

namespace OneShelf.Videos.App;

public class Service2
{
    private readonly GooglePhotosService _googlePhotosService;
    private readonly ILogger<Service2> _logger;

    public Service2(GooglePhotosService googlePhotosService, ILogger<Service2> logger)
    {
        _googlePhotosService = googlePhotosService;
        _logger = logger;
    }

    public async Task LoginAndListAlbums()
    {
        if (!await _googlePhotosService.LoginAsync())
            throw new("login failed");

        var albums = await _googlePhotosService.GetAlbumsAsync();
        foreach (var album in albums)
        {
            _logger.LogInformation($"{album.id}\t{album.title}");
        }
    }
}