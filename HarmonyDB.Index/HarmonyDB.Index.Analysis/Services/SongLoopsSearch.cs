using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

/// <summary>
/// The loops of one song, and where a chosen one of them is in the song.
/// </summary>
public class SongLoopsSearch
{
    private readonly ChordDataParser _chordDataParser;
    private readonly ProgressionsBuilder _progressionsBuilder;
    private readonly IndexExtractor _indexExtractor;
    private readonly InputParser _inputParser;
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly ProgressionsVisualizer _progressionsVisualizer;

    public SongLoopsSearch(ChordDataParser chordDataParser, ProgressionsBuilder progressionsBuilder, IndexExtractor indexExtractor, InputParser inputParser, ProgressionsSearch progressionsSearch, ProgressionsVisualizer progressionsVisualizer)
    {
        _chordDataParser = chordDataParser;
        _progressionsBuilder = progressionsBuilder;
        _indexExtractor = indexExtractor;
        _inputParser = inputParser;
        _progressionsSearch = progressionsSearch;
        _progressionsVisualizer = progressionsVisualizer;
    }

    /// <summary>
    /// The song's progression, built from its chords (in the chord data form, as <see cref="ChordDataParser.GetProgressionData"/> takes them), and its loops.
    /// </summary>
    public (List<StructureLink> loops, ChordsProgression progression) FindLoops(IEnumerable<string> chords)
    {
        var progression = _progressionsBuilder.BuildProgression(chords.Select(_chordDataParser.GetProgressionData).ToList());
        var loops = _indexExtractor.FindStructureLinks("does not matter", progression.Compact());
        return (loops, progression);
    }

    /// <summary>
    /// Where the loop is in the progression, as the custom attributes of the chords (see <see cref="ProgressionsVisualizer.BuildCustomAttributesForSearch"/>):
    /// the loop's title is searched for in the progression.
    /// </summary>
    public IReadOnlyDictionary<int, string> BuildCustomAttributesForLoop(ChordsProgression progression, StructureLink loop)
    {
        var searchProgression = _inputParser.ParseSequence(loop.Normalized.GetTitle(loopify: true));
        var found = _progressionsSearch.Search(progression.Once().ToList(), searchProgression);

        return _progressionsVisualizer.BuildCustomAttributesForSearch(progression, found);
    }
}
