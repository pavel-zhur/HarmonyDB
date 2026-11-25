using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.Api.Functions.VExternal1;
using OneShelf.Common.Api.Common;
using Xunit;

namespace HarmonyDB.Index.Api.Tests;

/// <summary>
/// A song's analysis by its id: a song the index has not analysed is "not found" (answered with
/// <see cref="CacheItemNotFoundException.StatusCode"/>), not a failure of the index.
/// </summary>
public sealed class TonalitiesSongTests
{
    [Fact]
    public void ASongNotAnalysedIsNotFound()
    {
        var ids = new IndexIds();
        var analysed = ids.Songs.GetOrAdd("analysed-1");
        var partly = ids.Songs.GetOrAdd("partly-1");
        var structures = new IdPositions([analysed, partly]);
        var tonalities = new IdPositions([analysed]);

        Assert.Equal(analysed, TonalitiesSong.FindSong(ids, "analysed-1", structures, tonalities));
        Assert.Throws<CacheItemNotFoundException>(() => TonalitiesSong.FindSong(ids, "unknown-1", structures, tonalities));
        Assert.Throws<CacheItemNotFoundException>(() => TonalitiesSong.FindSong(ids, "partly-1", structures, tonalities));
    }
}
