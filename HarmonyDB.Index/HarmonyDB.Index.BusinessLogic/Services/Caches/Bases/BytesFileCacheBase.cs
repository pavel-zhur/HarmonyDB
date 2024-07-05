using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Services.Caches.Bases;

public abstract class BytesFileCacheBase<TPresentationModel> : FileCacheBase<byte[], TPresentationModel>
    where TPresentationModel : class
{
    protected BytesFileCacheBase(ILogger<FileCacheBase<byte[], TPresentationModel>> logger, IOptions<FileCacheBaseOptions> options) : base(logger, options)
    {
    }

    protected override async Task<byte[]?> StreamDeserialize(Stream stream)
    {
        using var result = new MemoryStream();
        await stream.CopyToAsync(result);
        return result.ToArray();
    }

    protected override async Task StreamSerialize(Stream stream, byte[] model)
    {
        await stream.WriteAsync(model);
    }
}