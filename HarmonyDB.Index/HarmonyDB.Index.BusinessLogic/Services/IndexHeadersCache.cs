using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class IndexHeadersCache : FileCacheBase<byte[], IndexHeaders>
{
    public IndexHeadersCache(ILogger<IndexHeadersCache> logger)
        : base(logger)
    {
    }

    protected override string Key => "IndexHeaders";

    public async Task Save(byte[] model)
    {
        await StreamCompressSerialize(model);
    }

    protected override IndexHeaders ToPresentationModel(byte[] fileModel)
        => IndexHeaders.Deserialize(fileModel);
}