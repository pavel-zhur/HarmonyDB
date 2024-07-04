using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Services;

public abstract class BytesFileCacheBase<TPresentationModel> : FileCacheBase<byte[], TPresentationModel>
    where TPresentationModel : class
{
    protected BytesFileCacheBase(ILogger<FileCacheBase<byte[], TPresentationModel>> logger, IOptions<FileCacheBaseOptions> options) : base(logger, options)
    {
    }

    protected override async Task<byte[]?> StreamDeserialize(Stream stream)
    {
        var result = new byte[stream.Length];
        var length = await stream.ReadAsync(result, 0, result.Length);
        if (length != stream.Length) throw new("Fewer bytes are read than the stream length. Could not have happened.");
        return result;
    }

    protected override async Task StreamSerialize(Stream stream, byte[] model)
    {
        await stream.WriteAsync(model);
    }
}