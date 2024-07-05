using HarmonyDB.Index.Analysis.Models.CompactV1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using HarmonyDB.Index.BusinessLogic.Models;
using HarmonyDB.Index.BusinessLogic.Services.Caches.Bases;

namespace HarmonyDB.Index.BusinessLogic.Services.Caches;

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

    protected override IReadOnlyDictionary<string, CompactChordsProgression> ToPresentationModel(IReadOnlyDictionary<string, byte[]> fileModel)
        => fileModel.ToDictionary(x => x.Key, x => CompactChordsProgression.Deserialize(new(x.Value)));
}