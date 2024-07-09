using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class ProgressionsCache : FileCacheBase<IReadOnlyDictionary<string, byte[]>, IReadOnlyDictionary<string, CompactChordsProgression>>
{
    public ProgressionsCache(ILogger<ProgressionsCache> logger, IOptions<FileCacheBaseOptions> options)
        : base(logger, options)
    {
    }

    protected override string Key => "Progressions";

    public async Task Save(IReadOnlyDictionary<string, byte[]> model)
    {
        await StreamCompressSerialize(model);
    }

    protected override async Task<IReadOnlyDictionary<string, CompactChordsProgression>> ToPresentationModel(
        IReadOnlyDictionary<string, byte[]> fileModel)
        => fileModel.ToDictionary(x => x.Key, x => CompactChordsProgression.Deserialize(new(x.Value)));
}