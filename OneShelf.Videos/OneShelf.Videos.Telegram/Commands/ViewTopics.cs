using Microsoft.EntityFrameworkCore;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Database;

namespace OneShelf.Videos.Telegram.Commands;

[AdminCommand("view_topics", "Топики", "Посмотреть топики")]
public class ViewTopics : Command
{
    private readonly VideosDatabase _videosDatabase;

    public ViewTopics(Io io, VideosDatabase videosDatabase) 
        : base(io)
    {
        _videosDatabase = videosDatabase;
    }

    protected override async Task ExecuteQuickly()
    {
        foreach (var topic in await _videosDatabase.Topics
                     .Include(x => x.LiveChat)
                     .Include(x => x.LiveTopic)
                     .Include(x => x.StaticChat)
                     .Include(x => x.StaticTopic)
                     .ToListAsync())
        {
            Io.WriteLine($"{(topic.LiveChat != null ? "L" : "S")}: {topic.LiveChat?.Title ?? topic.StaticChat!.Name} / {topic.LiveTopic?.Title ?? topic.StaticTopic!.Title}");
        }
    }
}