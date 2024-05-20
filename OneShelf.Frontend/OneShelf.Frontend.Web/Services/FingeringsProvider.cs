using System.Reflection;
using System.Text.Json;
using Nito.AsyncEx;
using OneShelf.Frontend.Web.Models.Fingerings;

namespace OneShelf.Frontend.Web.Services;

public class FingeringsProvider
{
    private FingeringsModelIndex? _cache;
    private readonly AsyncLock _lock = new();

    public ILookup<string, string> SynonymsTextToHtmls { get; }

    public IReadOnlyDictionary<string, string> SynonymsTextToText { get; }

    public FingeringsProvider()
    {
        var synonyms = new List<(string text, string synonymText, string synonymHtml)>
        {
            ("C+", "C5+", "C<sup>5+</sup>"),
            ("C+", "Caug", "Caug"),
            ("C75+", "Caug7", "Caug<sub>7</sub>"),
            ("Cmaj5-", "Cmaj75-", "Cmaj<sub>7</sub><sup>5-</sup>"),
            ("Cmaj", "Cmaj7", "Cmaj<sub>7</sub>"),
            ("Csus4", "Csus", "Csus"),
            ("C6sus4", "C6sus", "C<sub>6</sub>sus"),
            ("C7sus4", "C7sus", "C<sub>7</sub>sus"),
            ("Cm7+", "Cmmaj7", "Cmmaj<sub>7</sub>"),
            ("Cm5-", "Cdim", "Cdim"),
            ("Cadd9", "Cadd2", "Cadd<sub>2</sub>"),
            ("Cadd11", "Cadd4", "Cadd<sub>4</sub>"),
            ("Cmadd9", "Cmadd2", "Cmadd<sub>2</sub>"),
            ("Cmadd11", "Cmadd4", "Cmadd<sub>4</sub>"),
        };

        SynonymsTextToHtmls = synonyms.ToLookup(x => x.text, x => x.synonymHtml);
        SynonymsTextToText = synonyms.ToDictionary(x => x.synonymText, x => x.text);
    }

    public async Task<FingeringsModelIndex> GetModel()
    {
        if (_cache != null) return _cache;

        using var _ = await _lock.LockAsync();

        if (_cache != null) return _cache;
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OneShelf.Frontend.Web.Resources.fingerings.json")!;

        var model = await JsonSerializer.DeserializeAsync<FingeringsModel>(stream);
        _cache = new()
        {
            Model = model!,
            TypesByChordText = model.Types.ToDictionary(x => x.ChordText),
        };

        return _cache;
    }

    public string ReplaceTitle(string title) => title.Replace("w", "#").Replace("H", "B");
}