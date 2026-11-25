import { distinct, orderBy } from '../../common/dotnet/Linq';
import { byteParse, compareInts } from '../../common/dotnet/Numbers';
import { NoteAlteration } from '../../common/HarmonyDB.Common/NoteAlteration';
import { Note, RepresentationSettings } from '../../representations';
import type { ILogger } from '../../common/dotnet/Logging';
import { ChordType } from '../Models/ChordType';
import { ChordDataV1 } from '../Models/V1/ChordDataV1';
import { HarmonyDataV1 } from '../Models/V1/HarmonyDataV1';
import { chordTypeToString } from '../Tools/Extensions';

// The patterns are .NET regular expressions translated to JavaScript ones that match the same strings:
// - .NET's \d is any Unicode decimal digit in one UTF-16 code unit (.NET matches code units, so the digits outside the
//   BMP, written as surrogate pairs, are not \d): Digit, \p{Nd} limited to the BMP with the u flag;
// - .NET's $ (without RegexOptions.Multiline) matches at the end or before a final \n: (?=\n?$);
// - .NET's . is any character but \n (JavaScript's . also excludes \r, \u2028, \u2029): [^\n];
// - escapes the u flag does not allow are written without the backslash (\! is !, \# is #).
const Range = 24;
const Modulus = 12;
// (A positive lookahead for the BMP, (?=[\u0000-\uffff]), is not used: V8 stops matching it after the first run.)
const Digit = '(?:(?![\\u{10000}-\\u{10ffff}])\\p{Nd})';
const NotePattern = `\\[!(${Digit}+)([b#])?!\\]`;
const End = '(?=\\n?$)';
const AnyChar = '[^\\n]';

const notePatternGlobal = new RegExp(NotePattern, 'gu');
const bassPattern = new RegExp(`^(${AnyChar}+)\\/${NotePattern}${End}`, 'u');
const fingeringPattern = new RegExp(`^${NotePattern}(${AnyChar}*)${End}`, 'u');

/** (byte[] chord, byte root, byte? bass, ChordType chordTypeForCircleOfFifths, ChordType chordTypeForProgressions, string? fingering, NoteAlteration? rootAlteration, NoteAlteration? bassAlteration) */
export interface ChordTuple {
    readonly chord: number[];
    readonly root: number;
    readonly bass: number | null;
    readonly chordTypeForCircleOfFifths: ChordType;
    readonly chordTypeForProgressions: ChordType;
    readonly fingering: string | null;
    readonly rootAlteration: NoteAlteration | null;
    readonly bassAlteration: NoteAlteration | null;
}

/** (int[] bass, int[] main, int root, string? fingering) */
export interface ChordNotes {
    readonly bass: number[];
    readonly main: number[];
    readonly root: number;
    readonly fingering: string | null;
}

interface Test {
    readonly pattern: RegExp;
    readonly notes: () => number[];
    readonly chordTypeForCircleOfFifths: ChordType;
    readonly chordTypeForProgressions: ChordType;
}

/** (string pattern, Func<byte[]> notes, ChordType chordTypeForCircleOfFifths, ChordType chordTypeForProgressions) */
function test(pattern: string, notes: () => number[], chordTypeForCircleOfFifths: ChordType, chordTypeForProgressions: ChordType): Test {
    return { pattern: new RegExp(pattern, 'u'), notes, chordTypeForCircleOfFifths, chordTypeForProgressions };
}

const tests: readonly Test[] = [
    test(`^${NotePattern}${End}`, () => [0, 4, 7], ChordType.Major, ChordType.Major),
    test(`^${NotePattern}${Digit}*add${Digit}+${End}`, () => [0, 4, 7], ChordType.Major, ChordType.Major),
    test(`^${NotePattern}m${End}`, () => [0, 3, 7], ChordType.Minor, ChordType.Minor),
    test(`^${NotePattern}m${Digit}*add${Digit}+${End}`, () => [0, 3, 7], ChordType.Minor, ChordType.Minor),

    test(`^${NotePattern}5${End}`, () => [0, 7], ChordType.Unknown, ChordType.Power),

    test(`^${NotePattern}[7913]+${End}`, () => [0, 4, 7, 10], ChordType.Major, ChordType.Major),
    test(`^${NotePattern}m[7913]+${End}`, () => [0, 3, 7, 10], ChordType.Minor, ChordType.Minor),

    test(`^${NotePattern}${Digit}*sus2${End}`, () => [0, 2, 7], ChordType.Unknown, ChordType.Sus2),
    test(`^${NotePattern}m${Digit}*sus2${End}`, () => [0, 2, 7], ChordType.Minor, ChordType.Minor),
    test(`^${NotePattern}${Digit}*sus4?${End}`, () => [0, 5, 7], ChordType.Unknown, ChordType.Sus4),
    test(`^${NotePattern}m${Digit}*sus4?${End}`, () => [0, 5, 7], ChordType.Minor, ChordType.Minor),

    test(`^${NotePattern}m5${End}`, () => [0, 7], ChordType.Unknown, ChordType.Unknown), // eq 5
    test(`^${NotePattern}m\\+${End}`, () => [0, 7], ChordType.Unknown, ChordType.Unknown), // eq 5

    test(`^${NotePattern}maj[7913]+${End}`, () => [0, 4, 7, 11], ChordType.Major, ChordType.Major),
    test(`^${NotePattern}\\+[7913]+${End}`, () => [0, 4, 7, 11], ChordType.Major, ChordType.Unknown),
    test(`^${NotePattern}[7913]+\\+${End}`, () => [0, 4, 7, 11], ChordType.Major, ChordType.Major),
    test(`^${NotePattern}maj${End}`, () => [0, 4, 7, 11], ChordType.Major, ChordType.Major),
    test(`^${NotePattern}maj`, () => [0, 4, 7, 11], ChordType.Major, ChordType.Unknown), // all starting with (to avoid mixing with m)
    test(`^${NotePattern}mmaj[7913]+${End}`, () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
    test(`^${NotePattern}m\\+[7913]+${End}`, () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
    test(`^${NotePattern}m[7913]+\\+${End}`, () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
    test(`^${NotePattern}mmaj${End}`, () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
    test(`^${NotePattern}mmaj`, () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor), // all starting with (to avoid mixing with m)

    test(`^${NotePattern}m6${End}`, () => [0, 3, 7, 9], ChordType.Minor, ChordType.Minor),
    test(`^${NotePattern}6${End}`, () => [0, 4, 7, 9], ChordType.Major, ChordType.Major),

    test(`^${NotePattern}9${End}`, () => [0, 4, 7, 10], ChordType.Major, ChordType.Major), // will play 7
    test(`^${NotePattern}m9${End}`, () => [0, 3, 7, 10], ChordType.Minor, ChordType.Minor), // will play 7

    test(`^${NotePattern}[7913]+sus4${End}`, () => [0, 5, 7, 10], ChordType.Unknown, ChordType.Sus4),
    test(`^${NotePattern}[7913]+sus${End}`, () => [0, 5, 7, 10], ChordType.Unknown, ChordType.Sus4), // sus4
    test(`^${NotePattern}[7913]+sus2${End}`, () => [0, 2, 7, 10], ChordType.Unknown, ChordType.Sus2),

    test(`^${NotePattern}dim[7913]+${End}`, () => [0, 3, 6, 10], ChordType.Minor, ChordType.Diminished),
    test(`^${NotePattern}m[7913]+5-${End}`, () => [0, 3, 6, 10], ChordType.Minor, ChordType.Diminished),
    test(`^${NotePattern}m[7913]+-5${End}`, () => [0, 3, 6, 10], ChordType.Minor, ChordType.Diminished),
    test(`^${NotePattern}dim${End}`, () => [0, 3, 6], ChordType.Minor, ChordType.Diminished),
    test(`^${NotePattern}m-5${End}`, () => [0, 3, 6], ChordType.Minor, ChordType.Diminished),
    test(`^${NotePattern}m5-${End}`, () => [0, 3, 6], ChordType.Minor, ChordType.Diminished),

    test(`^${NotePattern}\\+${End}`, () => [0, 4, 8], ChordType.Major, ChordType.Unknown),
    test(`^${NotePattern}aug${End}`, () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
    test(`^${NotePattern}[7913]+#5${End}`, () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
    test(`^${NotePattern}[7913]+?5+${End}`, () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
    test(`^${NotePattern}#5${End}`, () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
    test(`^${NotePattern}${AnyChar}*5\\+${End}`, () => [0, 4, 8], ChordType.Unknown, ChordType.Unknown),

    test(`^${NotePattern}m`, () => [0, 3, 7], ChordType.Minor, ChordType.Unknown),
    test(`^${NotePattern}`, () => [0, 4, 7], ChordType.Major, ChordType.Unknown),
];

export class ChordDataParser {
    private readonly _chordDataCache = new Map<string, ChordTuple | null>();

    private readonly _logger: ILogger;

    constructor(logger: ILogger) {
        this._logger = logger;
    }

    getNotes(chordData: string): ChordNotes | null {
        const parsed = this.getChord(chordData);
        if (parsed === null) {
            return null;
        }

        const { chord, root, bass: maybeBass, fingering } = parsed;

        const bass = maybeBass ?? root;

        const main = orderBy(
            distinct([
                ...chord.map(x => x + root),
                ...chord.map(x => x + root - Modulus),
                ...chord.map(x => x + root + Modulus),
            ])
                .filter(x => x > bass)
                .filter(x => x < Range),
            compareInts);

        return {
            bass: [
                bass - Modulus,
                bass,
            ],
            main,
            root,
            fingering,
        };
    }

    getProgressionData(chordData: string): ChordDataV1 {
        const originalRepresentation = chordData.replace(
            notePatternGlobal,
            (_match, group1: string, group2: string | undefined) => new Note(byteParse(group1), ChordDataParser.parseNoteAlteration(group2 ?? '')).representation(new RepresentationSettings()));

        const chord = this.getChord(chordData);
        if (chord === null) {
            return new ChordDataV1({
                harmonyRepresentation: '?',
                originalRepresentation,
                chordData,
            });
        }

        const { root, bass, chordTypeForProgressions: chordType, rootAlteration, bassAlteration } = chord;
        return new ChordDataV1({
            harmonyRepresentation: `${new Note(root, rootAlteration).representation(new RepresentationSettings())}${chordTypeToString(chordType)}${bass !== null ? `/${new Note(bass, bassAlteration).representation(new RepresentationSettings())}` : ''}`,
            chordData,
            harmonyData: new HarmonyDataV1({
                root,
                alteration: rootAlteration,
                chordType,
            }),
            originalRepresentation,
        });
    }

    private static parseNoteAlteration(match: string): NoteAlteration | null {
        switch (match) {
            case '#':
                return NoteAlteration.Sharp;
            case 'b':
                return NoteAlteration.Flat;
            default:
                return null;
        }
    }

    getChord(chordData: string): ChordTuple | null {
        if (this._chordDataCache.has(chordData)) return this._chordDataCache.get(chordData)!;

        const value = this.parseChord(chordData);
        this._chordDataCache.set(chordData, value);
        return value;
    }

    private parseChord(chordData: string): ChordTuple | null {
        try {
            const bassMatch = bassPattern.exec(chordData);
            let bass: number | null = null;
            let bassAlteration: NoteAlteration | null = null;
            let rootData = chordData;
            if (bassMatch !== null) {
                bass = byteParse(bassMatch[2]!);
                bassAlteration = ChordDataParser.parseNoteAlteration(bassMatch[3] ?? '');
                rootData = bassMatch[1]!;
            }

            const fingeringMatch = fingeringPattern.exec(rootData);
            const rootAlteration = fingeringMatch !== null ? ChordDataParser.parseNoteAlteration(fingeringMatch[2] ?? '') : null;
            const fingering = fingeringMatch !== null ? fingeringMatch[3]! : null;

            const match = tests
                .map(x => ({ match: x.pattern.exec(rootData), notes: x.notes, chordTypeForCircleOfFifths: x.chordTypeForCircleOfFifths, chordTypeForProgressions: x.chordTypeForProgressions }))
                .find(x => x.match !== null);

            if (match === undefined) {
                this._logger.logWarning('Chord parsing failed: {}.', chordData);
                return null;
            }

            return {
                chord: match.notes(),
                root: byteParse(match.match![1]!),
                bass,
                chordTypeForCircleOfFifths: match.chordTypeForCircleOfFifths,
                chordTypeForProgressions: match.chordTypeForProgressions,
                fingering,
                rootAlteration,
                bassAlteration,
            };
        } catch (e) {
            this._logger.logError(e, 'Error parsing the chord data.');
            return null;
        }
    }
}
