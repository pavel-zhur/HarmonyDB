using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.BusinessLogic.Services;

namespace OneShelf.Videos.Telegram.Processor.Commands;

[AdminCommand("list_albums", "Альбомы", "Посмотреть альбомы")]
public class ListAlbums : Command
{
    private readonly Service2 _service2;

    public ListAlbums(Io io, Service2 service2) 
        : base(io)
    {
        _service2 = service2;
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(List());
    }

    private async Task List()
    {
        var albums = await _service2.ListAlbums();
        foreach (var album in albums)
        {
            Io.WriteLine(album);
        }
    }
}