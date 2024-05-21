using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Services;

public class AutoUploader
{
    private readonly IllustrationsCosmosDatabase _illustrationsCosmosDatabase;
    private readonly StorageAccountUploader _storageAccountUploader;
    private readonly ImagesProcessor _imagesProcessor;
    private readonly ILogger<AutoUploader> _logger;

    public AutoUploader(ILogger<AutoUploader> logger, IllustrationsCosmosDatabase illustrationsCosmosDatabase, StorageAccountUploader storageAccountUploader, ImagesProcessor imagesProcessor)
    {
        _illustrationsCosmosDatabase = illustrationsCosmosDatabase;
        _storageAccountUploader = storageAccountUploader;
        _imagesProcessor = imagesProcessor;
        _logger = logger;
    }

    public async Task Go(Guid? id = null)
    {
        if (!_storageAccountUploader.IsConfigAvailable)
        {
            _logger.LogInformation("My illustrations upload config is unavailable.");
            return;
        }

        var illustrations = id.HasValue
            ? (await _illustrationsCosmosDatabase.GetIllustration(id.Value))!.Once().ToList() 
            : await _illustrationsCosmosDatabase.GetIllustrationsWithoutPublicUrls();

        foreach (var illustration in illustrations)
        {
            var sizes = new[]
            {
                1024,
                512,
                256,
                128,
            };

            var resizes = _imagesProcessor.Resize(illustration.Image, sizes);
            var urls = new List<Uri>();

            _logger.LogInformation("Uploading {id}", illustration.Id);
            foreach (var (size, resize) in sizes.Zip(resizes))
            {
                urls.Add(await _storageAccountUploader.Upload(resize, $"{illustration.Id}.{size}.jpg"));
            }

            _logger.LogInformation("Saving {id}", illustration.Id);
            await _illustrationsCosmosDatabase.AddIllustrationPublicUrls(
                illustration.Id,
                urls[0],
                urls[1],
                urls[2],
                urls[3]);
        }
    }
}