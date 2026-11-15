using HarmonyDB.Common.Fingerings.Models;

namespace HarmonyDB.Common.Fingerings.Tests;

public class FingeringsProviderTests
{
    private readonly FingeringsProvider _provider = new();

    [Fact]
    public async Task ModelLoads()
    {
        var model = await _provider.GetModel();

        Assert.Equal(56, model.Model.Types.Count);
        Assert.All(model.Model.Types, t => Assert.Equal(12, t.Notes.Count));
        Assert.All(model.Model.Types.SelectMany(t => t.Notes.Values).SelectMany(x => x), p => Assert.True(model.Model.Pictures.ContainsKey(p)));
        Assert.Same(model, await _provider.GetModel());
    }

    [Fact]
    public void Synonyms()
    {
        Assert.Equal(14, _provider.SynonymsTextToText.Count);
        Assert.Equal("Cm5-", _provider.SynonymsTextToText["Cdim"]);
        Assert.Equal(["C<sup>5+</sup>", "Caug"], _provider.SynonymsTextToHtmls["C+"]);
        Assert.Empty(_provider.SynonymsTextToHtmls["Cm"]);
    }

    [Fact]
    public async Task Pictures()
    {
        var model = await _provider.GetModel();

        // D minor: the type "Cm" on the note D.
        var dm = _provider.GetPictures(model, (int)FingeringsNote.D, "m");
        Assert.NotEmpty(dm);
        Assert.Equal(model.TypesByChordText["Cm"].Notes[FingeringsNote.D].Select(x => model.Model.Pictures[x]), dm);

        // a synonym: dim is m5-.
        Assert.Equal(_provider.GetPictures(model, 0, "m5-"), _provider.GetPictures(model, 0, "dim"));

        // not known.
        Assert.Empty(_provider.GetPictures(model, 0, "xyz"));
    }

    [Fact]
    public async Task Search()
    {
        var model = await _provider.GetModel();

        Assert.Equal("m7", _provider.NormalizeSearch(" Am7 "));
        Assert.Equal("sus", _provider.NormalizeSearch("sus"));
        Assert.Null(_provider.NormalizeSearch(null));

        var all = model.Model.Types.Where(t => _provider.IsMatch(t, _provider.NormalizeSearch("C"))).ToList();
        Assert.Equal(model.Model.Types.Count, all.Count);

        // by a synonym only: "dim" finds Cm5- and Cdim7 (by its own name).
        var dim = model.Model.Types.Where(t => _provider.IsMatch(t, _provider.NormalizeSearch("dim"))).Select(t => t.ChordText).ToList();
        Assert.Contains("Cm5-", dim);
        Assert.Contains("Cdim7", dim);
    }

    [Fact]
    public void ReplaceTitle()
    {
        Assert.Equal("C#", _provider.ReplaceTitle(FingeringsNote.Cw.ToString()));
        Assert.Equal("B", _provider.ReplaceTitle(FingeringsNote.H.ToString()));
        Assert.Equal("Bb", _provider.ReplaceTitle(FingeringsNote.Bb.ToString()));
    }
}
