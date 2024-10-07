using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.BusinessLogic.Services;
using OneShelf.Videos.BusinessLogic.Services.GooglePhotosExtensions.NonInteractive;

namespace OneShelf.Videos.Telegram.Commands;

[AdminCommand("list_albums", "Альбомы", "Посмотреть альбомы")]
public class ListAlbums(Io io, Service2 service2, ILogger<ListAlbums> logger) : Command(io)
{
    protected override async Task ExecuteQuickly()
    {
        Scheduled(List());
    }

    private async Task List()
    {
        try
        {
            var albums = await service2.ListAlbums();

            foreach (var album in albums)
            {
                Io.WriteLine(album);
            }
        }
        catch (NonInteractiveAuthenticationNeededException)
        {
            Io.WriteLine("Нужен человеческий логин.");
        }
    }
}