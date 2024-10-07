using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.BusinessLogic.Models;
using OneShelf.Videos.BusinessLogic.Services.Live;
using OneShelf.Videos.Database;

namespace OneShelf.Videos.Telegram.Commands;

[AdminCommand("s1_updatelive", "Скачать", "Шаг 1 - обновить базу и скачать новые")]
public class Step1DownloadLive(Io io, ILogger<Step1DownloadLive> logger, LiveDownloader liveDownloader, VideosDatabase videosDatabase) : Command(io)
{
    protected override async Task ExecuteQuickly()
    {
        Scheduled(Run());
    }

    private async Task Run()
    {
        var statistics = new LiveDownloaderStatistics();
        try
        {
            await liveDownloader.UpdateLive(true, statistics);
            await videosDatabase.AppendTopics();
            await videosDatabase.AppendMediae();
            await videosDatabase.UpdateMediaTopics();
            Io.WriteLine("Success.");
        }
        catch (Exception e)
        {
            logger.LogError(e);
            Io.WriteLine($"{e.GetType()} {e.Message}");
        }
        finally
        {
            Io.WriteLine($"Chats: {statistics.Chats}");
            Io.WriteLine($"Topics: {statistics.Topics}");
            Io.WriteLine($"Mediae: {statistics.Mediae}");
            Io.WriteLine($"ToDownload: {statistics.ToDownload}");
            Io.WriteLine($"Downloaded: {statistics.Downloaded}");
        }
    }
}