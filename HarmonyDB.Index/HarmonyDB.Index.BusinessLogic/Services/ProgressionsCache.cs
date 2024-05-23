using HarmonyDB.Index.Analysis.Models.CompactV1;
using Microsoft.Extensions.Logging;
using System.IO;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class ProgressionsCache : FileCacheBase<IReadOnlyDictionary<string, byte[]>, IReadOnlyDictionary<string, CompactChordsProgression>>
{
    public ProgressionsCache(ILogger<ProgressionsCache> logger)
        : base(logger)
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