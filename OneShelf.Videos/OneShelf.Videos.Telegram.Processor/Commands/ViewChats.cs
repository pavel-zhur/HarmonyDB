using Microsoft.EntityFrameworkCore;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Database;

namespace OneShelf.Videos.Telegram.Processor.Commands;

[AdminCommand("view_chats", "Чаты", "Посмотреть чаты")]
public class ViewChats : Command
{
    private readonly VideosDatabase _videosDatabase;

    public ViewChats(Io io, VideosDatabase videosDatabase) 
        : base(io)
    {
        _videosDatabase = videosDatabase;
    }

    protected override async Task ExecuteQuickly()
    {
        foreach (var chat in await _videosDatabase.Chats.ToListAsync())
        {
            Io.WriteLine(chat.Name);
        }
    }
}