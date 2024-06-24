using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;

namespace HarmonyDB.Index.Api.Services;

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

        var sequence = chordsProgression.ExtendedHarmonyMovementsSequences.Single();

        Console.WriteLine(string.Join("   ", sequence.Movements.Select(m => m.Title)));

        return sequence;
    }
}