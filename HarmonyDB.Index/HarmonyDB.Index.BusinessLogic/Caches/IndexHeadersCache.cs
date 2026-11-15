using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class IndexHeadersCache : BytesFileCacheBase<IndexHeaders>
{
    private readonly IndexIds _ids;

    public IndexHeadersCache(ILogger<IndexHeadersCache> logger, IOptions<FileCacheBaseOptions> options, IndexIds ids)
        : base(logger, options)
    {
        _ids = ids;
    }

    protected override string Key => "IndexHeaders";

    public async Task Save(byte[] model)
    {
        await StreamCompressSerialize(model);
    }

    protected override IndexHeaders ReadPresentationModel(Stream stream)
        => IndexHeaders.Read(stream, _ids.Songs);
}