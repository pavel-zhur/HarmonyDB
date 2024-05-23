using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace HarmonyDB.Index.BusinessLogic.Services;

public abstract class FileCacheBase<TFileModel, TPresentationModel>
    where TPresentationModel : class
{
    private readonly AsyncLock _lock = new();
    private Cache? _cache;

    protected FileCacheBase(ILogger<FileCacheBase<TFileModel, TPresentationModel>> logger)
    {
        Logger = logger;
    }

    protected ILogger<FileCacheBase<TFileModel, TPresentationModel>> Logger { get; }

    protected abstract string Key { get; }

    private string FileName => $"{Key}Cache.bin";

    protected abstract TPresentationModel ToPresentationModel(TFileModel fileModel);

    protected async Task StreamCompressSerialize(TFileModel model)
    {
        using var _ = await _lock.LockAsync();
        await using var file = File.OpenWrite(FileName);
        await using var gzip = new GZipStream(file, CompressionMode.Compress);
        await JsonSerializer.SerializeAsync(gzip, model);
        _cache = new()
        {
            Data = ToPresentationModel(model),
        };
    }

    private async Task<TFileModel> StreamDecompressDeserialize()
    {
        await using var file = File.OpenRead(FileName);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<TFileModel>(gzip)!;
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
            if (!File.Exists(FileName))
            {
                _cache = new()
                {
                    Data = null,
                };
            }

            _cache = new()
            {
                Data = ToPresentationModel(await StreamDecompressDeserialize()),
            };
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