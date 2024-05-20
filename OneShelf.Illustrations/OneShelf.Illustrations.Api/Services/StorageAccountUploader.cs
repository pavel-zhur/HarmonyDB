using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Illustrations.Api.Options;

namespace OneShelf.Illustrations.Api.Services;

public class StorageAccountUploader
{
    private readonly IllustrationsApiOptions _options;
    private readonly ILogger<StorageAccountUploader> _logger;

    public StorageAccountUploader(IOptions<IllustrationsApiOptions> options, ILogger<StorageAccountUploader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigAvailable => !string.IsNullOrWhiteSpace(_options.StorageAccountPrivateUrl) &&
                                     !string.IsNullOrWhiteSpace(_options.StorageAccountPublicUrl);

    public async Task<Uri> Upload(byte[] bytes, string fileName)
    {
        try
        {
            if (!IsConfigAvailable)
            {
                throw new(
                    $"{nameof(_options.StorageAccountPrivateUrl)} and {_options.StorageAccountPublicUrl} are required in the config.");
            }

            var client = new BlobContainerClient(new(_options.StorageAccountPrivateUrl!));

            try
            {
                using var memoryStream = new MemoryStream(bytes);
                await client.UploadBlobAsync(fileName, memoryStream);

                var blobClient = client.GetBlobClient(fileName);
                await blobClient.SetHttpHeadersAsync(new()
                {
                    ContentType = "image/jpeg",
                    CacheControl = "max-age=31536000",
                });
            }
            catch (RequestFailedException e) when (e.Status == 409) // already exists
            {
            }

            return new(new(_options.StorageAccountPublicUrl!), fileName);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error uploading a file to blob: {fileName}", fileName);
            throw;
        }
    }
}