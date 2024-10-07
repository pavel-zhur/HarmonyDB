using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Videos.Telegram.Commands;

[AdminCommand("get_file_size", "Файлик", "Посмотреть файл")]
public class GetFileSize : Command
{
    public GetFileSize(Io io) 
        : base(io)
    {
    }

    protected override async Task ExecuteQuickly()
    {
        var path = Io.FreeChoice("Path to file (\\ will be replaced with /):");
        Io.WriteLine(new FileInfo(path.Replace('\\', '/')).Length.ToString());
    }
}