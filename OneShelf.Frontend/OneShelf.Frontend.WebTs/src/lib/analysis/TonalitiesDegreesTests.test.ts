// HarmonyDB.Index.Analysis.Tests/TonalitiesDegreesTests.cs

import { describe, expect, it } from 'vitest';
import { range } from '../common/dotnet/Linq';
import { Note, RepresentationSettings } from '../representations';
import type { ILogger } from '../common/dotnet/Logging';
import { ChordType } from './Models/ChordType';
import { ChordDataParser } from './Services/ChordDataParser';
import { InputParser } from './Services/InputParser';
import { ProgressionsBuilder } from './Services/ProgressionsBuilder';
import { chordTypeToString } from './Tools/Extensions';
import { type FunctionTonality, type Tonality, toFunction, toRequiredChordValue, tryParseBestTonality } from './Tools/TonalitiesExtensions';

/** C#'s implicit conversion of the (root, isMinor) tuple to ToFunction's (tonic, isMinor). */
function asFunctionTonality(tonality: Tonality): FunctionTonality {
    return { tonic: tonality.root, isMinor: tonality.isMinor };
}

const output: string[] = [];
const logger: ILogger = {
    logInformation: message => output.push(message),
    logWarning: message => output.push(message),
    logError: (_, message) => output.push(message),
};

const inputParser = new InputParser(new ProgressionsBuilder(logger), new ChordDataParser(logger));

const chordTypes = Object.values(ChordType).filter((x): x is ChordType => typeof x === 'number');

describe('TonalitiesDegreesTests', () => {
    it('All', () => {
        const combinations = range(0, 12).flatMap(r =>
            chordTypes.flatMap(c =>
                range(0, 12).flatMap(t =>
                    range(0, 2).map(m => ({
                        chord: `${new Note(r).representation(new RepresentationSettings())}${chordTypeToString(c)}`,
                        tonality: `${new Note(t).representation(new RepresentationSettings())}${m === 1 ? 'm' : ''}`,
                    })))));

        logger.logInformation(combinations
            .map(c => `(${c.chord}, ${c.tonality}) -> ${toFunction(
                toRequiredChordValue(inputParser
                    .parseChord(c.chord)),
                asFunctionTonality(tryParseBestTonality(c.tonality)!))}`)
            .join('\n'));
    });

    // Ⅰ, Ⅱ, Ⅲ, Ⅳ, Ⅴ, Ⅵ, Ⅶ
    // ⅰ, ⅱ, ⅲ, ⅳ, ⅴ, ⅵ, ⅶ
    it.each([
        ['Am', 'Am', 'ⅰ'],
        ['C', 'Am', 'Ⅲ'],
        ['Cm', 'Am', 'ⅲ'],
        ['Bbdim', 'Am', '♭ⅱdim'],
    ])('Test(%s, %s, %s)', (chord, tonality, expected) => {
        expect(
            toFunction(
                toRequiredChordValue(inputParser
                    .parseChord(chord)),
                asFunctionTonality(tryParseBestTonality(tonality)!)))
            .toBe(expected);
    });
});
