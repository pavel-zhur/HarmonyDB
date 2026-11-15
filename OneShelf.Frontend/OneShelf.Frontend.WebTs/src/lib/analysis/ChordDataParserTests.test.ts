// HarmonyDB.Index.Analysis.Tests/ChordDataParserTests.cs

import { describe, expect, it } from 'vitest';
import { split, StringSplitOptions } from '../common/dotnet/text';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import type { ILogger } from '../common/dotnet/Logging';
import { ChordDataParser } from './Services/ChordDataParser';

const output: string[] = [];
const logger: ILogger = {
    logInformation: message => output.push(message),
    logWarning: message => output.push(message),
    logError: (_, message) => output.push(message),
};

const chordDataParser = new ChordDataParser(logger);

const Tests = `[!5!]
[!5!]m
[!5!]7
[!5!]5
[!5!]m7
[!5!]/[!5!]
[!5!]sus2
[!5!]m/[!5!]
[!5!]add9
[!5!]sus4
[!5!]maj
[!5!]m5
[!5!]maj7
[!5!]m7/[!5!]
[!5!]m6
[!5!]6
[!5!]m9
[!5!]7/[!5!]
[!5!]7sus4
[!5!]sus
[!5!]dim7
[!5!]9
[!5!]dim
[!5!]madd9
[!5!]m75-
[!5!]add11+
[!5!]m6/[!5!]
[!5!]m7add11
[!5!]maj9
[!5!]+7
[!5!]m7-5
[!5!]m(V)
[!5!]maj/[!5!]
[!5!]msus4
[!5!]add11
[!5!]9-
[!5!]m+
[!5!]madd11
[!5!]7+
[!5!]m(VI)
[!5!]7(V)
[!5!]*
[!5!]m-5
[!5!]7sus2
[!5!]m+7
[!5!]add13
[!5!]7+5-
[!5!](III)
[!5!]msus
[!5!]6add9
[!5!](VIII)
[!5!]7(VII)
[!5!]madd9/[!5!]
[!5!]sus2/[!5!]
[!5!]msus2
[!5!]m7sus2
[!5!]m11
[!5!]7/
[!5!]7+5/[!5!]
[!5!]aug
[!5!]m(VII)
[!5!]add9/[!5!]
[!5!]m7+
[!5!]5-
[!5!]mmaj7
[!5!]4
[!5!]majadd11+
[!5!]9sus4
[!5!]add9+
[!5!]7add11
[!5!]sus4/[!5!]
[!5!]6sus4
[!5!]+
[!5!]m7/5-/[!5!]
[!5!]7add13
[!5!]2
[!5!]add4add9
[!5!]m75-add11
[!5!]maj5-
[!5!]75+
[!5!]9+
[!5!]79+
[!5!]maj9/[!5!]
[!5!]11
[!5!]maj7/[!5!]
[!5!]m#
[!5!]dim7|
[!5!]+/[!5!]
[!5!]add9-
[!5!]msus2add11+
[!5!]79-
[!5!]6/[!5!]
[!5!]m6add11
[!5!]maj6
[!5!]m7add11/[!5!]
[!5!]m5-
[!5!](VI)
[!5!]min
[!5!]7b9
[!5!]7#5
[!5!]7sus4/[!5!]
[!5!]7/5-/[!5!]
[!5!]7/5-
[!5!]m13
[!5!]7sus
[!5!]1
[!5!]-8
[!5!]+5
[!5!]VD
[!5!]-2222
[!5!]m9/[!5!]
[!5!]?
[!5!]msus4/[!5!]
[!5!]add13-
[!5!]m7b5`;

describe('ChordDataParserTests', () => {
    it('GetChord', () => {
        // The C# splits by Environment.NewLine, the line breaks of the verbatim string.
        for (const x of split(Tests, ['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            const result = chordDataParser.getChord(x);
            if (result !== null) {
                expect(result.root).toBe(5);
                output.push(`${x.padEnd(20)}\t\tb=${result.bass ?? ''}\t${result.chord.join(', ')}`);
            } else {
                output.push(x);
            }
        }
    });

    it('GetAlteration', () => {
        expect(chordDataParser.getChord('[!5b!]xxx/[!3#!]')!.rootAlteration).toBe(NoteAlteration.Flat);
        expect(chordDataParser.getChord('[!5#!]xxx/[!3b!]')!.rootAlteration).toBe(NoteAlteration.Sharp);
        expect(chordDataParser.getChord('[!5!]xxx/[!3!]')!.rootAlteration).toBe(null);
        expect(chordDataParser.getChord('[!5b!]xxx/[!3#!]')!.bassAlteration).toBe(NoteAlteration.Sharp);
        expect(chordDataParser.getChord('[!5#!]xxx/[!3b!]')!.bassAlteration).toBe(NoteAlteration.Flat);
        expect(chordDataParser.getChord('[!5!]xxx/[!3!]')!.bassAlteration).toBe(null);
    });
});
