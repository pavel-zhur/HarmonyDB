import { single } from '../../common/dotnet/Linq';
import { split, StringSplitOptions, trim } from '../../common/dotnet/text';
import { NodeChord, NodeHtml, NodeText, RepresentationSettings } from '../../representations';
import type { HarmonyMovementsSequence } from '../Models/HarmonyMovementsSequence';
import type { ChordDataV1 } from '../Models/V1/ChordDataV1';
import type { ChordDataParser } from './ChordDataParser';
import type { ProgressionsBuilder } from './ProgressionsBuilder';

export class InputParser {
    private readonly _progressionsBuilder: ProgressionsBuilder;
    private readonly _chordDataParser: ChordDataParser;

    constructor(progressionsBuilder: ProgressionsBuilder, chordDataParser: ChordDataParser) {
        this._progressionsBuilder = progressionsBuilder;
        this._chordDataParser = chordDataParser;
    }

    parseChord(input: string): ChordDataV1 {
        const html = new NodeHtml({
            name: 'pre',
            children:
            [
                new NodeChord({
                    children: [new NodeText(trim(input)).asChild()],
                }).asChild(),
            ],
        });

        return this._chordDataParser.getProgressionData(single(html.asChords(new RepresentationSettings())));
    }

    parseSequence(input: string): HarmonyMovementsSequence {
        const html = new NodeHtml({
            name: 'pre',
            children: split(input, [' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .map(c => new NodeChord({
                    children: [new NodeText(c).asChild()],
                }).asChild()),
        });

        const chordsProgression =
            this._progressionsBuilder.buildProgression(html.asChords(new RepresentationSettings()).map(x => this._chordDataParser.getProgressionData(x)));

        return single(chordsProgression.extendedHarmonyMovementsSequences);
    }
}
