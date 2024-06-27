using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;

namespace HarmonyDB.Index.Analysis.Services;

public class InputParser
{
    private readonly ProgressionsBuilder _progressionsBuilder;
    private readonly ChordDataParser _chordDataParser;

    public InputParser(ProgressionsBuilder progressionsBuilder, ChordDataParser chordDataParser)
    {
        _progressionsBuilder = progressionsBuilder;
        _chordDataParser = chordDataParser;
    }

    public HarmonyMovementsSequence Parse(string input)
    {
        var html = new NodeHtml
        {
            Name = "pre",
            Children = input
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => new NodeChord
                {
                    Children = [new NodeText(c).AsChild()]
                }.AsChild())
            .ToList(),
        };

        var chordsProgression =
            _progressionsBuilder.BuildProgression(html.AsChords(new()).Select(_chordDataParser.GetProgressionData)
                .ToList());

        return chordsProgression.ExtendedHarmonyMovementsSequences.Single();
    }
}