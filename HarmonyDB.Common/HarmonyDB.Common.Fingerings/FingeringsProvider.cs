using System.Reflection;
using System.Text.Json;
using HarmonyDB.Common.Fingerings.Models;
using Nito.AsyncEx;

namespace HarmonyDB.Common.Fingerings;

/// <summary>
/// The guitar fingerings of the chords: the pictures of each chord type on each note, and the synonyms of the chord type names.
/// The chord types are named as chords on C (e.g. "C", "Cm7", "Csus4"), the notes are <see cref="FingeringsNote"/>.
/// </summary>
public class FingeringsProvider
{
    private FingeringsModelIndex? _cache;
    private readonly AsyncLock _lock = new();

    /// <summary>
    /// A chord type name (as in the model) to the html of its synonyms.
    /// </summary>
    public ILookup<string, string> SynonymsTextToHtmls { get; }

    /// <summary>
    /// A synonym of a chord type name to the chord type name (as in the model).
    /// </summary>
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
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HarmonyDB.Common.Fingerings.Resources.fingerings.json")!;

        var model = await JsonSerializer.DeserializeAsync<FingeringsModel>(stream);
        _cache = new()
        {
            Model = model!,
            TypesByChordText = model.Types.ToDictionary(x => x.ChordText),
        };

        return _cache;
    }

    /// <summary>
    /// The pictures of a chord: its chord type is found by "C" + <paramref name="fingering"/>, or through the synonyms;
    /// then the pictures of the type on the <paramref name="note"/> (a <see cref="FingeringsNote"/>, 0 is A).
    /// Empty if the chord type is not found.
    /// </summary>
    public IReadOnlyList<byte[]> GetPictures(FingeringsModelIndex model, int note, string fingering)
    {
        if (!model.TypesByChordText.TryGetValue("C" + fingering, out var type))
        {
            if (SynonymsTextToText.TryGetValue("C" + fingering, out var text))
            {
                type = model.TypesByChordText[text];
            }
            else
            {
                return new List<byte[]>();
            }
        }

        return type.Notes[(FingeringsNote)note].Select(x => model.Model.Pictures[x]).ToList();
    }

    /// <summary>
    /// The search text as <see cref="IsMatch"/> takes it: lowercase, trimmed, without the leading note letter.
    /// </summary>
    public string? NormalizeSearch(string? search)
    {
        search = search?.ToLowerInvariant().Trim();
        if ("abcdefgh".Any(x => search?.StartsWith(x) ?? false)) search = search!.Substring(1);
        return search;
    }

    /// <summary>
    /// Whether the chord type is found by the search text (normalized by <see cref="NormalizeSearch"/>), by its name or its synonyms.
    /// </summary>
    public bool IsMatch(FingeringsChordType type, string? search) =>
        string.IsNullOrWhiteSpace(search)
        || type.ChordText.Contains(search)
        || SynonymsTextToText.Any(y => y.Key.Contains(search) && y.Value == type.ChordText);

    public string ReplaceTitle(string title) => title.Replace("w", "#").Replace("H", "B");
}
