using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class SongHeadersCache : FileCacheBase<byte[], SongHeaders>
{
    public SongHeadersCache(ILogger<SongHeadersCache> logger)
        : base(logger)
    {
    }

    protected override string Key => "SongHeaders";

    public async Task Save(byte[] model)
    {
        await StreamCompressSerialize(model);
    }

    protected override SongHeaders ToPresentationModel(byte[] fileModel)
        => SongHeaders.Deserialize(fileModel);
}