using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.BusinessLogic.Services;

namespace OneShelf.Videos.Telegram.Commands;

[AdminCommand("s2_uploadlive", "Залить", "Шаг 2 - залить незалитые")]
public class Step2UploadLive(Io io, ILogger<Step2UploadLive> logger, Service1 service1, Service2 service2) : Command(io)
{
    protected override async Task ExecuteQuickly()
    {
        Scheduled(Run());
    }

    private async Task Run()
    {
        try
        {
            var exportLivePhoto = await service1.GetExportLivePhoto();
            var exportLiveVideo = await service1.GetExportLiveVideo();

            Io.WriteLine($"photos: {exportLivePhoto.Count}");
            Io.WriteLine($"videos: {exportLiveVideo.Count}");

            await service2.UploadPhotos(exportLivePhoto);
            await service2.UploadVideos(exportLiveVideo);

            Io.WriteLine("Success.");
        }
        catch (Exception e)
        {
            logger.LogError(e);
            Io.WriteLine($"{e.GetType()} {e.Message}");
        }
    }
}