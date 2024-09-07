using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Database;

namespace OneShelf.Videos.Telegram.Processor.Commands;

[AdminCommand("get_file_size", "Файлик", "Посмотреть файл")]
public class GetFileSize : Command
{
    private readonly VideosDatabase _videosDatabase;

    public GetFileSize(Io io, VideosDatabase videosDatabase) 
        : base(io)
    {
        _videosDatabase = videosDatabase;
    }

    protected override async Task ExecuteQuickly()
    {
        var path = Io.FreeChoice("Path to file:");
        Io.WriteLine(new FileInfo(path).Length.ToString());
    }
}