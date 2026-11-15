import { csEquals } from '../../common/dotnet/Equality';
import { ArgumentException, KeyNotFoundException } from '../../common/dotnet/exceptions';
import { atFromEnd, contains, distinct, first, groupBy, single, singleOrDefault, takeWhile } from '../../common/dotnet/Linq';
import type { ILogger } from '../../common/dotnet/Logging';
import { asPairs, once, toChunks, withIndices, withIsLast } from '../../common/OneShelf.Common/Extensions';
import { Note, RepresentationSettings } from '../../representations';
import { ChordsProgression } from '../Models/ChordsProgression';
import { ChordType } from '../Models/ChordType';
import { ChordTypePresentation } from '../Models/ChordTypePresentation';
import { HarmonyGroup } from '../Models/HarmonyGroup';
import { HarmonyMovement } from '../Models/HarmonyMovement';
import { HarmonyMovementsSequence } from '../Models/HarmonyMovementsSequence';
import type { ChordDataV1 } from '../Models/V1/ChordDataV1';
import { HarmonyDataV1 } from '../Models/V1/HarmonyDataV1';
import { chordTypeToString } from '../Tools/Extensions';

interface SiblingStandards {
    readonly current: ChordDataV1;
    readonly previousStandard: ChordDataV1 | null;
    readonly previousStandardIndex: number | null;
    readonly nextStandard: ChordDataV1 | null;
}

interface Want extends SiblingStandards {
    readonly compatibleWithPrevious: boolean;
    readonly compatibleWithNext: boolean;
    readonly previousAndNextAreCompatible: boolean;
}

interface GoTo {
    readonly sameGroup: boolean;
    readonly goToPrevious: boolean;
    readonly goToNext: boolean;
    readonly incompatible: boolean;
}

/** ToDictionary: throws on a duplicate key. */
function toDictionary<T, K, V>(items: Iterable<T>, keySelector: (item: T) => K, valueSelector: (item: T) => V): Map<K, V> {
    const result = new Map<K, V>();
    for (const item of items) {
        const key = keySelector(item);
        if (result.has(key)) throw new ArgumentException(`An item with the same key has already been added. Key: ${String(key)}`);
        result.set(key, valueSelector(item));
    }

    return result;
}

function getOrThrow<K, V>(dictionary: ReadonlyMap<K, V>, key: K): V {
    if (!dictionary.has(key)) throw new KeyNotFoundException(key);
    return dictionary.get(key)!;
}

class Promise<T> {
    value: T | null = null;
}

export class ProgressionsBuilder {
    private readonly _logger: ILogger;

    constructor(logger: ILogger) {
        this._logger = logger;
    }

    buildProgression(sequence: readonly ChordDataV1[], buildStandardToo = false): ChordsProgression {
        const siblingStandards = ProgressionsBuilder.buildSiblingStandards(sequence);

        const wants: Want[] = siblingStandards.map((x): Want => ({
            current: x.current,
            previousStandard: x.previousStandard,
            previousStandardIndex: x.previousStandardIndex,
            nextStandard: x.nextStandard,
            compatibleWithPrevious: x.current.isStandard
                ? csEquals(x.current.harmonyData, x.previousStandard?.harmonyData ?? null)
                : x.current.harmonyData !== null && x.previousStandard !== null && x.previousStandard.harmonyData!.root === x.current.harmonyData.root,
            compatibleWithNext: x.current.isStandard
                ? csEquals(x.current.harmonyData, x.nextStandard?.harmonyData ?? null)
                : x.current.harmonyData !== null && x.nextStandard !== null && x.nextStandard.harmonyData!.root === x.current.harmonyData.root,
            previousAndNextAreCompatible: x.previousStandard !== null && x.nextStandard !== null && csEquals(x.previousStandard.harmonyData, x.nextStandard.harmonyData),
        }));

        const nonStandardsGoTo = toDictionary(
            groupBy(withIndices(wants).filter(x => !x.x.current.isStandard), x => x.x.previousStandardIndex)
                .map(g => g.items)
                .map(c => {
                    const empty = c.slice(0, 0).map(x => x.i);

                    // all want to go to next
                    if (c.every(x => x.x.compatibleWithPrevious === true && x.x.compatibleWithNext === false))
                        return {
                            c,
                            sameGroup: false,
                            goToPrevious: c.map(x => x.i),
                            incompatible: empty,
                            goToNext: empty,
                        };

                    // all want to go to previous
                    if (c.every(x => x.x.compatibleWithPrevious === false && x.x.compatibleWithNext === true))
                        return {
                            c,
                            sameGroup: false,
                            goToPrevious: empty,
                            incompatible: empty,
                            goToNext: c.map(x => x.i),
                        };

                    // all want to go neither ways
                    if (c.every(x => x.x.compatibleWithPrevious === false && x.x.compatibleWithNext === false))
                        return {
                            c,
                            sameGroup: false,
                            goToPrevious: empty,
                            incompatible: c.map(x => x.i),
                            goToNext: empty,
                        };

                    // all want to go both ways
                    if (c.every(x => x.x.compatibleWithPrevious === true && x.x.compatibleWithNext === true && x.x.previousAndNextAreCompatible === true))
                        return {
                            c,
                            sameGroup: true,
                            goToPrevious: empty,
                            incompatible: empty,
                            goToNext: empty,
                        };

                    // all want to go both ways but they are incompatible
                    if (c.every(x => x.x.compatibleWithPrevious === true && x.x.compatibleWithNext === true && x.x.previousAndNextAreCompatible === false))
                        return {
                            c,
                            sameGroup: false,
                            goToPrevious: empty,
                            incompatible: c.map(x => x.i),
                            goToNext: empty,
                        };

                    const goToPrevious = takeWhile(c, x => x.x.compatibleWithPrevious).map(x => x.i);
                    const goToNext = takeWhile([...c].reverse(), x => x.x.compatibleWithNext).slice(0, Math.max(0, c.length - goToPrevious.length)).reverse().map(x => x.i);
                    return {
                        c,
                        sameGroup: false,
                        goToPrevious,
                        incompatible: c.slice(goToPrevious.length).slice(0, Math.max(0, c.length - goToPrevious.length - goToNext.length)).map(x => x.i),
                        goToNext,
                    };
                })
                .flatMap(c => c.c.map(x => ({ x, c }))),
            x => x.x.i,
            (x): GoTo => ({
                sameGroup: x.c.sameGroup,
                goToPrevious: contains(x.c.goToPrevious, x.x.i),
                goToNext: contains(x.c.goToNext, x.x.i),
                incompatible: contains(x.c.incompatible, x.x.i),
            }));

        const harmonySequence: HarmonyGroup[] = [];
        let originalSequence: ChordDataV1[] = [];
        let harmonyData: HarmonyDataV1 | null = null;
        let minChordIndex: number | null = null;
        let maxChordIndex: number | null = null;

        const complete = () => {
            if (minChordIndex === null) return;

            if (originalSequence.length === 0)
                throw new Error('Could not have happened.');

            let harmonyRepresentation: string;
            if (harmonyData !== null) {
                harmonyRepresentation = `${new Note(harmonyData.root, harmonyData.alteration).representation(new RepresentationSettings())}${chordTypeToString(harmonyData.chordType)}`;
            } else {
                const recognized = singleOrDefault(groupBy(
                    originalSequence.filter(x => x.harmonyData !== null),
                    x => ({ root: x.harmonyData!.root, alteration: x.harmonyData!.alteration })));

                if (recognized !== null) {
                    const root = new Note(recognized.key.root, recognized.key.alteration).representation(new RepresentationSettings());
                    const types = distinct(recognized.items.map(x => x.harmonyData!.chordType));
                    const chordType = types.length === 1 ? single(types) : ChordType.Unknown;
                    harmonyRepresentation = `${root}${chordTypeToString(chordType)}`;
                    harmonyData = new HarmonyDataV1({
                        root: recognized.key.root,
                        alteration: recognized.key.alteration,
                        chordType,
                    });
                } else {
                    harmonyRepresentation = '?';
                }
            }

            harmonySequence.push(new HarmonyGroup({
                index: harmonySequence.length,
                startChordIndex: minChordIndex,
                endChordIndex: maxChordIndex!,
                originalSequence,
                harmonyData,
                harmonyRepresentation,
            }));

            originalSequence = [];
            harmonyData = null;
            minChordIndex = maxChordIndex = null;
        };

        for (const { x: { current }, i } of withIndices(wants)) {
            let isComplete: boolean;
            if (current.isStandard) {
                isComplete =
                    harmonyData === null
                        ? originalSequence.length > 0 && ((x: GoTo) => x.goToPrevious || x.incompatible)(getOrThrow(nonStandardsGoTo, i - 1))
                        : !csEquals(harmonyData, current.harmonyData);
            } else {
                const { sameGroup, goToPrevious, goToNext, incompatible } = getOrThrow(nonStandardsGoTo, i);
                if (sameGroup || goToPrevious) {
                    isComplete = false;
                } else if (incompatible) {
                    const previous = nonStandardsGoTo.get(i - 1);
                    isComplete = originalSequence.length > 0 &&
                        (previous === undefined
                            || !previous.incompatible
                            || ((x: ChordDataV1) => (x.harmonyData?.root ?? null) !== (current.harmonyData?.root ?? null) || (x.harmonyData?.alteration ?? null) !== (current.harmonyData?.alteration ?? null))(atFromEnd(originalSequence, 1)));
                } else if (goToNext) {
                    const previous = nonStandardsGoTo.get(i - 1);
                    isComplete = originalSequence.length > 0 &&
                        (previous === undefined || !previous.goToNext);
                } else
                    throw new Error('Could not have happened, one of the four flags should be set.');
            }

            if (isComplete)
                complete();

            originalSequence.push(current);
            minChordIndex = Math.min(i, minChordIndex ?? i);
            maxChordIndex = Math.max(i, maxChordIndex ?? i);
            if (current.isStandard) {
                harmonyData ??= current.harmonyData;
            }
        }

        complete();

        const { standardMovementsSequences, extendedMovementsSequences } = ProgressionsBuilder.getHarmonyMovementsSequencesBoth(harmonySequence, buildStandardToo);

        const { groupsAttribution: standardGroupsAttribution, chordsAttribution: standardChordsAttribution } = buildStandardToo
            ? ProgressionsBuilder.getSequenceAttributions(standardMovementsSequences!)
            : { groupsAttribution: null, chordsAttribution: null };

        const { groupsAttribution: extendedGroupsAttribution, chordsAttribution: extendedChordsAttribution } = ProgressionsBuilder.getSequenceAttributions(extendedMovementsSequences);

        return new ChordsProgression({
            harmonySequence: harmonySequence.map(x => ({
                harmonyGroup: x,
                standardMovementsSequence: standardGroupsAttribution?.get(x) ?? null,
                extendedMovementsSequence: extendedGroupsAttribution.get(x) ?? null,
            })),
            originalSequence: harmonySequence.flatMap(x => withIndices(x.originalSequence).map(y => ({
                original: y.x,
                harmonyGroup: x,
                indexInHarmonyGroup: y.i,
                standardMovementsSequence: (standardChordsAttribution?.get(x.startChordIndex + y.i) ?? null),
                extendedMovementsSequence: extendedChordsAttribution.get(x.startChordIndex + y.i) ?? null,
            }))),
            standardHarmonyMovementsSequences: standardMovementsSequences,
            extendedHarmonyMovementsSequences: extendedMovementsSequences,
        });
    }

    private static getSequenceAttributions(movementsSequences: readonly HarmonyMovementsSequence[]): { groupsAttribution: Map<HarmonyGroup, HarmonyMovementsSequence>; chordsAttribution: Map<number, HarmonyMovementsSequence> } {
        const sequencesAttribution = toDictionary(
            movementsSequences
                .flatMap(s => withIsLast(s.movements).flatMap(m => ((x: HarmonyGroup[]) => m.isLast ? [...x, m.x.to] : x)(once(m.x.from)).map(g => ({ g, s })))),
            x => x.g,
            x => x.s);

        const chordsAttribution = toDictionary(
            [...sequencesAttribution]
                .flatMap(s =>
                    withIndices(s[0].originalSequence).map(c => ({ chordIndex: c.i + s[0].startChordIndex, s }))),
            x => x.chordIndex,
            x => x.s[1]);

        return { groupsAttribution: sequencesAttribution, chordsAttribution };
    }

    private static getHarmonyMovementsSequencesBoth(sequence: readonly HarmonyGroup[], buildStandardToo: boolean): { standardMovementsSequences: readonly HarmonyMovementsSequence[] | null; extendedMovementsSequences: readonly HarmonyMovementsSequence[] } {
        return {
            standardMovementsSequences: buildStandardToo ? ProgressionsBuilder.getHarmonyMovementsSequences(sequence, ProgressionsBuilder.isValidStandard) : null,
            extendedMovementsSequences: ProgressionsBuilder.getHarmonyMovementsSequences(sequence, ProgressionsBuilder.isValidExtended),
        };
    }

    /** internal */
    static isValidExtended(g: HarmonyGroup): boolean {
        return g.harmonyData !== null;
    }

    private static isValidStandard(g: HarmonyGroup): boolean {
        return g.isStandard;
    }

    /** internal */
    static getHarmonyMovementsSequences(sequence: readonly HarmonyGroup[], isValid: (g: HarmonyGroup) => boolean): readonly HarmonyMovementsSequence[] {
        return toChunks(asPairs(sequence), pair => isValid(pair.previous) && isValid(pair.current))
            .filter(c => c.criterium)
            .map(c => new HarmonyMovementsSequence({
                firstRoot: first(c.chunk).previous.harmonyData!.root,
                movements: c.chunk.map(p => {
                    const delta = Note.normalize(p.current.harmonyData!.root - p.previous.harmonyData!.root);
                    const titleDelta = delta > 7 ? delta - Note.Modulus : delta;

                    return new HarmonyMovement({
                        rootDelta: delta,
                        from: p.previous,
                        to: p.current,
                        fromType: p.previous.harmonyData!.chordType,
                        toType: p.current.harmonyData!.chordType,
                        title: `${titleDelta > 0 ? '+' : ''}${titleDelta} ${chordTypeToString(p.previous.harmonyData!.chordType, ChordTypePresentation.MajorAsM)}-${chordTypeToString(p.current.harmonyData!.chordType, ChordTypePresentation.MajorAsM)}`,
                    });
                }),
            }));
    }

    private static buildSiblingStandards(sequence: readonly ChordDataV1[]): SiblingStandards[] {
        let previousStandard: ChordDataV1 | null = null;
        let previousStandardIndex: number | null = null;
        let currentPromise = new Promise<ChordDataV1>();
        const withPreviousAndNextPromise: { current: ChordDataV1; previousStandard: ChordDataV1 | null; previousStandardIndex: number | null; nextStandard: Promise<ChordDataV1> | null }[] = [];

        for (const { x: current, i } of withIndices(sequence)) {
            if (current.isStandard) {
                currentPromise.value = current;
                currentPromise = new Promise<ChordDataV1>();
                withPreviousAndNextPromise.push({ current, previousStandard, previousStandardIndex, nextStandard: currentPromise });
                previousStandard = current;
                previousStandardIndex = i;
            } else {
                withPreviousAndNextPromise.push({ current, previousStandard, previousStandardIndex, nextStandard: currentPromise });
            }
        }

        return withPreviousAndNextPromise
            .map(x => ({ current: x.current, previousStandard: x.previousStandard, previousStandardIndex: x.previousStandardIndex, nextStandard: x.nextStandard?.value ?? null }));
    }
}
