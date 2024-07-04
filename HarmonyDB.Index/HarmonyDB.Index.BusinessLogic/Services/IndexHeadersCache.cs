using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class IndexHeadersCache : BytesFileCacheBase<IndexHeaders>
{
    public IndexHeadersCache(ILogger<IndexHeadersCache> logger, IOptions<FileCacheBaseOptions> options)
        : base(logger, options)
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