// TonalitiesExtensions. C# overloads have distinct names here:
//   ToIndex(byte, bool) -> toIndex;                              ToIndex(this (byte, bool)) -> toIndexOfTuple
//   GetMajorTonic(this (byte, bool), bool) -> getMajorTonic;     GetMajorTonic(byte, bool, bool) -> getMajorTonicOf
//   GetRelativeScale(this (byte, bool), bool) -> getRelativeScale; GetRelativeScale(byte, bool, bool) -> getRelativeScaleOf
//   GetTitle(this string, (byte, bool)?, bool, bool) -> getTitle
//   GetTitle(this string, byte, bool) -> getTitleFromBeginningNote
//   GetTitle(this string, (byte, bool), byte, bool) -> getTitleWithPredictedAndBeginningNote
//   GetTitle(this ReadOnlyMemory<CompactHarmonyMovement>, (byte, bool)?, byte, bool, bool) -> getTitleOfSequence
//   GetFunctionsTitle(this string, ...) -> getFunctionsTitle;    GetFunctionsTitle(this ReadOnlyMemory<...>, ...) -> getFunctionsTitleOfSequence
//   ToChord(this (byte, ChordType)) -> toChord;                  ToChord(byte, ChordType) -> toChordOf
//   ToSongTonalityTitle(this int) -> toSongTonalityTitleOfIndex; ToSongTonalityTitle(this (byte, bool)) -> toSongTonalityTitle
//   ToLoopTonalityTitle(this (byte, bool), byte?) -> toLoopTonalityTitleWithNormalizationRoot
//   ToLoopTonalityTitle(this int, (byte, bool)?) -> toLoopTonalityTitleOfIndex
//   ToLoopTonalityTitle(this (byte, bool), (byte, bool)?) -> toLoopTonalityTitleWithPredicted
//   ToLoopTonalityTitle(this (byte, bool)) -> toLoopTonalityTitle
// The `out` parameters are Out<T> holders (optional where the C# has an overload without them).
// C# converts a tuple to one of the same element types with other names implicitly; here the object is rebuilt with
// the names of the target (see getFunctionsTitleOfSequence).

import { ArgumentOutOfRangeException, IndexOutOfRangeException, InvalidOperationException } from '../../common/dotnet/exceptions';
import { compareNumbers, first, maxFloat, orderBy, range } from '../../common/dotnet/Linq';
import { intDiv, intMod, toByte, toFloat } from '../../common/dotnet/Numbers';
import { Out } from '../../common/dotnet/Out';
import { once } from '../../common/OneShelf.Common/Extensions';
import { Note, RepresentationSettings } from '../../representations';
import { type KnownTonality, Scale } from '../HarmonyDB.Index.Analysis.Em/Models';
import { ChordType } from '../Models/ChordType';
import { ChordTypePresentation } from '../Models/ChordTypePresentation';
import type { CompactHarmonyMovement } from '../Models/CompactV1/CompactHarmonyMovement';
import type { ChordDataV1 } from '../Models/V1/ChordDataV1';
import { chordTypeToString, loopify as loopifyExtension, shiftLoop as shiftLoopExtension } from './Extensions';
import { deserializeLoop } from './LoopSerializationExtensions';

/** (byte root, bool isMinor) */
export interface Tonality {
    readonly root: number;
    readonly isMinor: boolean;
}

/** (byte note, ChordType chordType) */
export interface Chord {
    readonly note: number;
    readonly chordType: ChordType;
}

/** (byte tonic, bool isMinor): the tonality parameter of ToFunction. */
export interface FunctionTonality {
    readonly tonic: number;
    readonly isMinor: boolean;
}

const Cache = new Map<string, Tonality | null>();

export const ProbabilitiesLength = Note.Modulus * 2;

export function toLinear(probabilities: readonly Float32Array[]): Float32Array {
    const linearArray = new Float32Array(ProbabilitiesLength);

    for (let root = 0; root < Note.Modulus; root++) {
        for (let i = 0; i < 2; i++) {
            const isMinor = i === 1;
            const index = toIndex(root, isMinor);
            linearArray[index] = probabilities[root]![isMinor ? 1 : 0]!;
        }
    }

    return linearArray;
}

export function toIndex(root: number, isMinor: boolean): number {
    return root * 2 + (isMinor ? 1 : 0);
}

export function toIndexOfTuple(index: Tonality): number {
    return toIndex(index.root, index.isMinor);
}

export function fromIndex(index: number): Tonality {
    return { root: toByte(intDiv(index, 2)), isMinor: intMod(index, 2) === 1 };
}

export function getPredictedTonality(probabilities: ArrayLike<number>, confidence: Out<number> = new Out<number>()): Tonality {
    const predictedTonality = first(
        orderBy(Array.from(probabilities, (probability, index) => ({ probability, index })), (a, b) => compareNumbers(b.probability, a.probability))
            .map(x => ({ x, tonality: fromIndex(x.index) })));

    confidence.value = predictedTonality.x.probability;
    return predictedTonality.tonality;
}

export function getSecondPredictedTonality(probabilities: ArrayLike<number>, confidence: Out<number> = new Out<number>()): Tonality {
    const result = first(
        orderBy(Array.from(probabilities, (probability, index) => ({ probability, index })), (a, b) => compareNumbers(b.probability, a.probability))
            .slice(1)
            .map(x => ({ probability: x.probability, tonality: fromIndex(x.index) })));

    confidence.value = result.probability;
    return result.tonality;
}

export function getMajorTonic(scale: Tonality, isSong: boolean): number {
    return scale.isMinor ? getRelativeScale(scale, isSong).root : scale.root;
}

export function getRelativeScale(scale: Tonality, isSong: boolean): Tonality {
    return scale.isMinor
        ? { root: Note.normalize(scale.root + (isSong ? 3 : -3)), isMinor: false }
        : { root: Note.normalize(scale.root + (isSong ? -3 : 3)), isMinor: true };
}

export function getMajorTonicOf(root: number, isMinor: boolean, isSong: boolean): number {
    return getMajorTonic({ root, isMinor }, isSong);
}

export function getRelativeScaleOf(root: number, isMinor: boolean, isSong: boolean): Tonality {
    return getRelativeScale({ root, isMinor }, isSong);
}

export function tonalityConfidence(probabilities: ArrayLike<number>): number {
    return maxFloat(Array.from(probabilities));
}

export function tonicConfidence(probabilities: ArrayLike<number>, isSong: boolean): number {
    const values = Array.from(probabilities);
    return maxFloat(range(0, Note.Modulus)
        .map(x => toFloat(arrayItem(values, toIndex(x, true)) + arrayItem(values, toIndexOfTuple(getRelativeScaleOf(x, true, isSong))))));
}

export function getAmCTonic(predicted: Tonality): number {
    return predicted.isMinor ? predicted.root : Note.normalize(predicted.root + 3);
}

export function getAmCRoot(predicted: Tonality): Tonality {
    return { root: predicted.isMinor ? 0 : 3, isMinor: predicted.isMinor };
}

export function toRequiredChordValue(chordDataV1: ChordDataV1): Chord {
    return { note: chordDataV1.harmonyData!.root, chordType: chordDataV1.harmonyData!.chordType };
}

export function getTitle(normalized: string, predicted: Tonality | null, loopify = false, shiftLoop = true): string {
    return predicted !== null
        ? getTitleOfSequence(deserializeLoop(normalized), getAmCRoot(predicted), getAmCTonic(predicted), loopify, shiftLoop)
        : getTitleFromBeginningNote(normalized, 0, loopify);
}

export function getTitleFromBeginningNote(normalized: string, beginningNote = 0, loopify = false): string {
    return getTitleOfSequence(deserializeLoop(normalized), null, beginningNote, loopify, false);
}

export function getTitleWithPredictedAndBeginningNote(normalized: string, predicted: Tonality, beginningNote: number, loopify = false): string {
    return getTitleOfSequence(deserializeLoop(normalized), predicted, beginningNote, loopify);
}

export function getTitleOfSequence(sequence: readonly CompactHarmonyMovement[], predicted: Tonality | null, beginningNote = 0, loopify = false, shiftLoop = true): string {
    return toSequence(sequence, predicted, beginningNote, new Out<number | null>(), loopify, shiftLoop)
        .map(x => toChord(x))
        .join(' ');
}

export function getFunctionsTitle(normalized: string, predicted: Tonality, loopify = false, shiftLoop = true): string {
    return getFunctionsTitleOfSequence(deserializeLoop(normalized), predicted, loopify, shiftLoop);
}

export function getFunctionsTitleOfSequence(sequence: readonly CompactHarmonyMovement[], predicted: Tonality, loopify = false, shiftLoop = true): string {
    const beginningNote = getAmCTonic(predicted);
    predicted = getAmCRoot(predicted);
    const tonality: FunctionTonality = { tonic: predicted.root, isMinor: predicted.isMinor };
    return toSequence(sequence, predicted, beginningNote, new Out<number | null>(), loopify, shiftLoop)
        .map(x => toFunction(x, tonality))
        .join(' ');
}

export function toSequence(
    sequence: readonly CompactHarmonyMovement[],
    predicted: Tonality | null,
    beginningNote: number,
    shift: Out<number | null>,
    loopify = false,
    shiftLoop = true,
): Chord[] {
    if (sequence.length === 0) throw new IndexOutOfRangeException();
    const chords: Chord[] = [
        ...once<Chord>({ note: beginningNote, chordType: sequence[0]!.fromType }),
        ...sequence
            .slice(0, sequence.length - 1)
            .map((m): Chord => {
                beginningNote = Note.normalize(beginningNote + m.rootDelta);
                return { note: beginningNote, chordType: m.toType };
            }),
    ];

    return loopifyExtension(
        shiftLoopExtension(
            chords,
            shiftLoop && predicted !== null,
            x =>
                x.note !== predicted!.root
                    ? 0
                    : (predicted!.isMinor ? ChordType.Minor : ChordType.Major) === x.chordType
                        ? 2
                        : 1,
            shift),
        loopify);
}

export function toChord(chord: Chord): string {
    return toChordOf(chord.note, chord.chordType);
}

export function toChordOf(note: number, chordType: ChordType): string {
    return `${new Note(note).representation(new RepresentationSettings())}${chordTypeToString(chordType)}`;
}

function nullableValue<T>(value: T | null): T {
    if (value === null) throw new InvalidOperationException('Nullable object must have a value.');
    return value;
}

function arrayItem<T>(array: readonly T[], index: number): T {
    if (index < 0 || index >= array.length) throw new IndexOutOfRangeException();
    return array[index]!;
}

export function toFunction(chord: Chord, tonality: FunctionTonality): string {
    const majorDegrees: (number | null)[] = [1, null, 2, null, 3, 4, null, 5, null, 6, null, 7];
    const minorDegrees: (number | null)[] = [1, null, 2, 3, null, 4, null, 5, 6, null, 7, null];
    const majorRomanDegrees = [...'ⅠⅡⅢⅣⅤⅥⅦ'];
    const minorRomanDegrees = [...'ⅰⅱⅲⅳⅴⅵⅶ'];
    const unknownRomanDegrees = [...'1234567'];
    const majorScaleDefaultIsMinorDegrees: (boolean | null)[] = [false, true, true, false, false, true, null];
    const minorScaleDefaultIsMinorDegrees: (boolean | null)[] = [true, null, false, true, true, false, false];
    const signFlat = '♭';

    const shift = Note.normalize(chord.note - tonality.tonic);
    const degrees = tonality.isMinor ? minorDegrees : majorDegrees;
    const defaultIsMinorDegrees = tonality.isMinor ? minorScaleDefaultIsMinorDegrees : majorScaleDefaultIsMinorDegrees;

    const unstableDegree = arrayItem(degrees, shift);
    let flat = false;
    let degree: number;
    if (unstableDegree === null) {
        degree = shift === degrees.length - 1 ? nullableValue(arrayItem(degrees, 0)) : nullableValue(arrayItem(degrees, shift + 1));
        flat = true;
    } else {
        degree = unstableDegree;
    }

    let romanDegrees: string[];
    switch (chord.chordType) {
        case ChordType.Major:
            romanDegrees = majorRomanDegrees;
            break;
        case ChordType.Minor:
            romanDegrees = minorRomanDegrees;
            break;
        case ChordType.Diminished:
            romanDegrees = minorRomanDegrees;
            break;
        case ChordType.Augmented:
            romanDegrees = majorRomanDegrees;
            break;
        case ChordType.Unknown:
        case ChordType.Power:
        case ChordType.Sus2:
        case ChordType.Sus4:
            if (unstableDegree === null) {
                romanDegrees = unknownRomanDegrees;
            } else {
                switch (arrayItem(defaultIsMinorDegrees, degree - 1)) {
                    case null:
                        romanDegrees = unknownRomanDegrees;
                        break;
                    case true:
                        romanDegrees = minorRomanDegrees;
                        break;
                    case false:
                        romanDegrees = majorRomanDegrees;
                        break;
                }
            }

            break;
        default:
            throw new ArgumentOutOfRangeException();
    }

    return (flat ? signFlat : '')
        + arrayItem(romanDegrees, degree - 1)
        + chordTypeToString(chord.chordType, ChordTypePresentation.Degrees);
}

/**
 * As in the C#, the cache is written under the string left after the first character is cut off (except when the first
 * character is not a note), so a later call with that rest returns the cached result.
 */
export function tryParseBestTonality(tonality: string): Tonality | null {
    const cachedResult = Cache.get(tonality);
    if (cachedResult !== undefined) {
        return cachedResult;
    }

    if (tonality.length === 0) throw new IndexOutOfRangeException();
    let note = Note.CharactersToNotes.get(tonality[0]!);
    if (note === undefined) {
        Cache.set(tonality, null);
        return null;
    }

    tonality = tonality.substring(1);
    let isMinor: boolean;

    switch (tonality) {
        case '#':
            note = note.sharp();
            isMinor = false;
            break;
        case 'b':
            note = note.flat();
            isMinor = false;
            break;
        case '#m':
            note = note.sharp();
            isMinor = true;
            break;
        case 'bm':
            note = note.flat();
            isMinor = true;
            break;
        case '':
            isMinor = false;
            break;
        case 'm':
            isMinor = true;
            break;
        default:
            Cache.set(tonality, null);
            return null;
    }

    const result: Tonality = { root: note.value, isMinor };
    Cache.set(tonality, result);
    return result;
}

export function getWeight(occurrences: number, successions: number, loopLength: number, isSongKnownTonality: boolean): number {
    return toFloat(toFloat(toFloat(occurrences + toFloat(successions * 4))
               * (loopLength === 2 ? 1 : 5))
               * (isSongKnownTonality ? 10 : 1));
}

export function fromEm(tonality: KnownTonality): Tonality {
    return { root: tonality.tonic, isMinor: tonality.scale === Scale.Minor };
}

export function toSongTonalityTitleOfIndex(tonalityIndex: number): string {
    return toSongTonalityTitle(fromIndex(tonalityIndex));
}

export function toSongTonalityTitle(tonality: Tonality): string {
    return `${new Note(tonality.root).representation(new RepresentationSettings())}${tonality.isMinor ? 'm' : ''}`;
}

export function toLoopTonalityTitleWithNormalizationRoot(tonality: Tonality, normalizationRoot: number | null): string {
    return normalizationRoot !== null
        ? toSongTonalityTitle({ root: Note.normalize(normalizationRoot - tonality.root), isMinor: tonality.isMinor })
        : toLoopTonalityTitle(tonality);
}

export function toLoopTonalityTitleOfIndex(tonalityIndex: number, predicted: Tonality | null = null): string {
    return toLoopTonalityTitleWithPredicted(fromIndex(tonalityIndex), predicted);
}

export function toLoopTonalityTitleWithPredicted(tonality: Tonality, predicted: Tonality | null): string {
    if (predicted === null) return toLoopTonalityTitle(tonality);

    let delta: number;
    switch (`${predicted.isMinor},${tonality.isMinor}`) {
        case 'true,true':
            delta = 0;
            break;
        case 'true,false':
            delta = 0;
            break;
        case 'false,true':
            delta = 3;
            break;
        default: // false,false
            delta = 3;
            break;
    }

    return toSongTonalityTitle({
        root: Note.normalize(
            predicted.root
            - tonality.root
            + delta),
        isMinor: tonality.isMinor,
    });
}

export function toLoopTonalityTitle(tonality: Tonality): string {
    return `${tonality.isMinor ? 'm' : 'M'}${toLoopTonalityShiftTitle(tonality.root)}`;
}

export function toLoopTonalityShiftTitle(root: number): string {
    return root >= LoopTonalityShiftsFirstNegativeRoot ? `–${-(root - 12)}` : `+${root}`;
}

export const LoopTonalityShiftsFirstNegativeRoot = 8;

export function getLoopTonalityShiftsDisplayOrder(predicted: Tonality | null): readonly number[] {
    return range(
        predicted !== null
            ? predicted.root - (predicted.isMinor ? 3 : 0)
            : LoopTonalityShiftsFirstNegativeRoot, Note.Modulus)
        .map(Note.normalize);
}
