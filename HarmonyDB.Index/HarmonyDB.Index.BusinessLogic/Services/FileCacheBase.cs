using System.IO.Compression;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace HarmonyDB.Index.BusinessLogic.Services;

public abstract class FileCacheBase<TFileModel, TPresentationModel>
    where TPresentationModel : class
    where TFileModel : class
{
    private readonly FileCacheBaseOptions _options;
    private readonly AsyncLock _lock = new();
    private readonly AsyncLock _containerClientLock = new();

    private Cache? _cache;
    private BlobContainerClient? _containerClient;
    private BlobServiceClient? _client;

    protected FileCacheBase(ILogger<FileCacheBase<TFileModel, TPresentationModel>> logger, IOptions<FileCacheBaseOptions> options)
    {
        _options = options.Value;
        Logger = logger;
    }

    protected ILogger<FileCacheBase<TFileModel, TPresentationModel>> Logger { get; }

    protected abstract string Key { get; }

    private string FileName => $"{Key}Cache.bin";
    
    private string FilePath => Path.Combine(_options.DiskPath ?? throw new("The file path is required in the options."), FileName);

    protected abstract TPresentationModel ToPresentationModel(TFileModel fileModel);

    public async Task Copy(Func<TFileModel, TFileModel>? updateFunc = null)
    {
        var x = await StreamDecompressDeserialize() ?? throw new("The data is not available.");
        if (updateFunc != null)
        {
            x = updateFunc(x);
        }

        await StreamCompressSerialize(x);
    }

    protected async Task StreamCompressSerialize(TFileModel model)
    {
        if (_options.WriteSource == FileCacheSource.Disk)
        {
            using var _ = await _lock.LockAsync();
            await using var file = File.OpenWrite(FilePath);
            await using var gzip = new GZipStream(file, CompressionMode.Compress);
            await JsonSerializer.SerializeAsync(gzip, model);
            _cache = new()
            {
                Data = ToPresentationModel(model),
            };
        }
        else if (_options.WriteSource == FileCacheSource.Storage)
        {
            var client = await GetContainerClient();
            var blobClient1 = client.GetBlobClient(FileName);
            using var _ = await _lock.LockAsync();
            var stream = await blobClient1.OpenWriteAsync(true);
            await using var gzip = new GZipStream(stream, CompressionMode.Compress);
            await JsonSerializer.SerializeAsync(gzip, model);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(_options.WriteSource), _options.WriteSource,
                "The write source is out of range.");
        }
    }

    private async Task<TFileModel?> StreamDecompressDeserialize()
    {
        if (_options.ReadSource == FileCacheSource.Disk)
        {
            if (!File.Exists(FilePath)) return null;

            await using var file = File.OpenRead(FilePath);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<TFileModel>(gzip)!;
        }

        if (_options.ReadSource == FileCacheSource.Storage)
        {
            try
            {
                var client = await GetContainerClient();
                var blobClient = client.GetBlobClient(FileName);
                var stream = await blobClient.OpenReadAsync();
                await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                return JsonSerializer.Deserialize<TFileModel>(gzip)!;
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                return null;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(_options.ReadSource), _options.ReadSource,
            "The write source is out of range.");
    }

    private async Task<BlobContainerClient> GetContainerClient()
    {
        if (_containerClient != null) return _containerClient;
        using var _ = await _containerClientLock.LockAsync();
        if (_containerClient != null) return _containerClient;
        _client ??= new(_options.StorageConnectionString);
        var containerClient = _client.GetBlobContainerClient("indexcache");
        await containerClient.CreateIfNotExistsAsync();
        return _containerClient = containerClient;
    }

    public void Initialize()
    {
        _ = Read();
    }

    public async Task<TPresentationModel> Get()
    {
        if (_cache == null)
        {
            await Read();
        }

        return _cache?.Data ?? throw new("The data does not exist.");
    }

    private async Task Read()
    {
        try
        {
            using var _ = await _lock.LockAsync();
            if (_cache != null) return;

            var started = DateTime.Now; 
            var fileModel = await StreamDecompressDeserialize();
            if (fileModel == null)
            {
                _cache = new()
                {
                    Data = null,
                };
            }

            Logger.LogInformation("Decompressing and deserializing the input model for the {type} memory cache took {time:N0} ms", Key, (DateTime.Now - started).TotalMilliseconds); 
            started = DateTime.Now;

            _cache = new()
            {
                Data = ToPresentationModel(fileModel),
            };

            Logger.LogInformation("Building the presentation model for the {type} memory cache took {time:N0} ms", Key, (DateTime.Now - started).TotalMilliseconds);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error reading the progressions cache.");
        }
    }

    private class Cache
    {
        public required TPresentationModel? Data { get; init; }
    }
}