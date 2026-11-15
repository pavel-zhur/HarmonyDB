// HarmonyDB.Index.Analysis.Tests/LoopExtractionTests.cs

import { describe, expect, it } from 'vitest';
import { groupBy, range, repeat, single } from '../common/dotnet/Linq';
import { toFloat } from '../common/dotnet/Numbers';
import { Out } from '../common/dotnet/Out';
import { split, StringSplitOptions } from '../common/dotnet/text';
import { Note, RepresentationSettings } from '../representations';
import type { ILogger } from '../common/dotnet/Logging';
import { anyDuplicates, asPairs } from '../common/OneShelf.Common/Extensions';
import { ChordType } from './Models/ChordType';
import { CompactHarmonyMovement } from './Models/CompactV1/CompactHarmonyMovement';
import { BlockTypes } from './Models/Index/BlockTypes';
import type { IBlock } from './Models/Index/Blocks/Interfaces/IBlock';
import { LoopBlock } from './Models/Index/Blocks/LoopBlock';
import type { LoopSelfJumpBlock } from './Models/Index/Blocks/LoopSelfJumpBlock';
import type { LoopSelfMultiJumpBlock } from './Models/Index/Blocks/LoopSelfMultiJumpBlock';
import { PingPongBlock } from './Models/Index/Blocks/PingPongBlock';
import { SequenceBlock } from './Models/Index/Blocks/SequenceBlock';
import { LoopSelfJumpType } from './Models/Index/Enums/LoopSelfJumpType';
import type { BlockGraph } from './Models/Index/Graphs/BlockGraph';
import { BlocksChartParameters } from './Models/TextGraphics/BlocksChartParameters';
import { ChordDataParser } from './Services/ChordDataParser';
import { IndexExtractor } from './Services/IndexExtractor';
import { PathsSearcher } from './Services/PathsSearcher';
import { ProgressionsBuilder } from './Services/ProgressionsBuilder';
import { ProgressionsVisualizer } from './Services/ProgressionsVisualizer';
import { chordTypeToString } from './Tools/Extensions';
import { deserializeLoop, serializeLoop } from './Tools/LoopSerializationExtensions';

const output: string[] = [];
const logger: ILogger = {
    logInformation: message => output.push(message),
    logWarning: message => output.push(message),
    logError: (_, message) => output.push(message),
};

const chordDataParser = new ChordDataParser(logger);
const progressionsBuilder = new ProgressionsBuilder(logger);
const pathsSearcher = new PathsSearcher();
const indexExtractor = new IndexExtractor(pathsSearcher);
const progressionsVisualizer = new ProgressionsVisualizer();

type Sequence = readonly CompactHarmonyMovement[];

/** Random.Shared.Next(minValue, maxValue). */
function next(minValue: number, maxValue: number): number {
    return minValue + Math.floor(Math.random() * (maxValue - minValue));
}

/** float.Epsilon */
const floatEpsilon = 1.401298464324817e-45;

/** Assert.Equal of two sequences of class instances: the same references in the same order. */
function expectSameSequence(expected: readonly unknown[], actual: readonly unknown[]): void {
    expect(actual.length).toBe(expected.length);
    expected.forEach((x, i) => expect(actual[i] === x).toBe(true));
}

/** Assert.Distinct: no two equal (here: the same reference) items. */
function expectDistinct(collection: readonly unknown[]): void {
    expect(new Set(collection).size).toBe(collection.length);
}

/** ValueTuple.ToString(): the elements in their order. */
function tupleToString(tuple: object): string {
    return `(${Object.values(tuple).join(', ')})`;
}

/** Assert.Equal of two value tuples: element by element, in order (the element names do not take part). */
function expectTupleEqual(expected: object, actual: object | null): void {
    expect(actual).not.toBeNull();
    expect(Object.values(actual!)).toEqual(Object.values(expected));
}

describe('LoopExtractionTests', () => {
    it.each([
        ['Am D G E Am D G E Am D G E Am D G E', 5], // normalized start of Am-D-G-E is D.
        ['Gm C F D Gm C F D Gm C F D Gm C F D', 3],
        ['Bb G E Am D G E Am D G E Am D G E', 5],
        ['Bb Am D G E Am D G E Am D G E Am D G E', 5],
        ['F# Gm C F D Gm C F D Gm C F D Gm C F D', 3],
    ])('NormalizationRootInterpretationCorrect(%s, %i)', (chordsInput, expectedNormalizedRoot) => {
        const { sequence, firstRoot } = inputChords(chordsInput);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);

        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(single(loops).normalizationRoot).toBe(expectedNormalizedRoot);
    });

    // The C# runs three workers on the thread pool until a token cancels after a second; JavaScript runs them one
    // after another on one thread, so the first worker has the second and the others find the token cancelled.
    it('HopeNoAmbiguousSelfJumps', async () => {
        const cancelAt = Date.now() + 1000;
        const isCancellationRequested = () => Date.now() >= cancelAt;
        const counters = await Promise.all(range(0, 3).map(async i => {
            await Promise.resolve();
            return worker(i);
        }));
        logger.logInformation(counters.join(', '));

        function worker(i: number): number {
            let counter = 0;
            while (!isCancellationRequested()) {
                counter++;
                let last = ChordType.Major;
                const getRandomChordType = (): ChordType => {
                    while (true) {
                        const nextType = next(ChordType.Major, ChordType.Augmented - 3) as ChordType;
                        if (nextType !== last) {
                            last = nextType;
                            return nextType;
                        }
                    }
                };

                let chordType = getRandomChordType();
                const sequence = range(0, 20).map(() => next(0, 12)).map(
                    d => new CompactHarmonyMovement({
                        rootDelta: d,
                        fromType: chordType,
                        toType: chordType = getRandomChordType(),
                    }));

                const firstRoot = next(0, 12);
                const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
                const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);

                if (loopSelfJumpsBlocks.length > 1) {
                    logger.logInformation(`${i}: ${loopSelfJumpsBlocks.length}`);
                    if (anyDuplicates(loopSelfJumpsBlocks
                        .flatMap(x => x.childLoops.map(x => ({ startIndex: x.startIndex, endIndex: x.endIndex }))))) {
                        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);
                        expect.fail('Found!');
                    }
                }
            }

            return counter;
        }
    });

    it('SelfJumpsDifferentDeltasNotFound', () => {
        const { sequence, firstRoot } = inputChords('C A C D C A C D C A C D C A C');
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);

        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);
        expect(loopSelfJumpsBlocks).toHaveLength(0);
    });

    it('SelfJumpsSameDeltaNoAmbiguity', () => {
        const { sequence, firstRoot } = inputChords(
            'C F C G C F C G C F C G C F C G C F C G C A#m' +
            ' C G C F C G C F C G C F C G C F C G C F C G C Bm' +
            ' F C G C F C G C F C G C F C G C F C G C Bm' +
            ' G C F C G C F C G C F C G C F C G C F C G C');
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);

        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);
        expect(loopSelfJumpsBlocks.length).toBe(4);

        // important: same normalization
        expect(new Set(loopSelfJumpsBlocks.map(x => x.normalized)).size).toBe(1);
    });

    it('NoneShort', () => {
        const { sequence, firstRoot } = inputDeltasParams(1);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);

        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops).toHaveLength(0);
    });

    it('NoneLong', () => {
        const { sequence, firstRoot } = inputDeltas(repeat(7, 11));
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops).toHaveLength(0);
    });

    it('NoneLongWithChordTypes', () => {
        let index = 0;
        const getFromType = (): ChordType => index === 0 ? ChordType.Augmented : Math.trunc((index - 1) / 12) as ChordType;
        const getToType = (): ChordType => Math.trunc(index++ / 12) as ChordType;

        const sequence = repeat(7, 12 * 8 - 1).map(d => {
            const fromType = getFromType();
            const toType = getToType();
            return new CompactHarmonyMovement({
                rootDelta: d,
                fromType,
                toType,
            });
        });

        const firstRoot = 0;
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops).toHaveLength(0);
    });

    it('OneLongWithChordTypes', () => {
        let index = 0;
        const getFromType = (): ChordType => index === 0 ? ChordType.Augmented : Math.trunc((index - 1) / 12) as ChordType;
        const getToType = (): ChordType => Math.trunc(index++ / 12) as ChordType;

        const sequence = repeat(7, 12 * 8).map(d => {
            const fromType = getFromType();
            const toType = getToType();
            return new CompactHarmonyMovement({
                rootDelta: d,
                fromType,
                toType,
            });
        });

        const firstRoot = 0;
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops).toHaveLength(1);
        expect([loops[0]!.startIndex, loops[0]!.endIndex]).toEqual([0, 12 * 8 - 1]);
    });

    it('OneLong', () => {
        const { sequence, firstRoot } = inputDeltas(repeat(7, 12), 0);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops).toHaveLength(1);
        expect([loops[0]!.startIndex, loops[0]!.endIndex]).toEqual([0, 11]);
    });

    it('EndingPlusOne', () => {
        const { sequence, firstRoot } = inputDeltas([...range(0, 100).map(() => next(1, 12)), 5, 7, 5, 7, 1]);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops[loops.length - 1]!.endIndex).toBe(103);
        expect(loops[loops.length - 1]!.loopLength).toBe(2);
    });

    it('Ending', () => {
        const { sequence, firstRoot } = inputDeltas([...range(0, 100).map(() => next(1, 12)), 5, 7, 5, 7]);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops[loops.length - 1]!.endIndex).toBe(103);
        expect(loops[loops.length - 1]!.loopLength).toBe(2);
    });

    it('Beginning', () => {
        const { sequence, firstRoot } = inputDeltas([5, 7, 5, 7, 5, ...range(0, 100).map(() => next(1, 12))]);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops[0]!.startIndex).toBe(0);
        expect(loops[0]!.loopLength).toBe(2);
        expect(loops[0]!.endIndex >= 4).toBe(true);
    });

    it('BeginningPlusOne', () => {
        const { sequence, firstRoot } = inputDeltas(
            [1, 5, 7, 5, 7, 5,
                ...range(0, 100).map(() => next(1, 12))],
            0);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops[0]!.startIndex).toBe(1);
        expect(loops[0]!.loopLength).toBe(2);
        expect(loops[0]!.endIndex >= 5).toBe(true);
    });

    it('Random100X100', () => {
        for (let i = 0; i < 100; i++) {
            const { sequence, firstRoot } = inputDeltas(range(0, 100).map(() => next(1, 12)));
            const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
            const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
            traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, false);
        }
    }, 600000);

    it('ChordsInputWithAlterations', () => {
        const sequenceChords = 'C# F# C# F# C# G# C# G# C# G# C# G# F# C# F# C# F# C# F#';
        const { sequence, firstRoot } = inputChords(sequenceChords);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loopSelfJumpsBlocks).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).childJumps.length).toBe(2);
        expect(single(loopSelfJumpsBlocks).childLoops.length).toBe(4);
    });

    it('GapLong', () => {
        const { sequence, firstRoot } = inputDeltasParams(
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            3, 4, 6,
            8, 4, 8, 4);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(2);
        expect(loops[1]!.startIndex - 1 > loops[0]!.endIndex).toBe(true);
    });

    it('GapImmediate', () => {
        const { sequence, firstRoot } = inputDeltasParams(
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            13,
            8, 4, 8, 4);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(2);
        expect(loops[1]!.startIndex - 1 > loops[0]!.endIndex).toBe(true);
    });

    it('Overlap', () => {
        const { sequence, firstRoot } = inputDeltasParams(
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 1, 2, 2, 1,
            2, 2, 1, 1, 1, 2, 2, 1,
            2, 2, 1, 1, 2, 1, 2, 1,
            2, 2, 1, 1, 2, 1, 2, 1);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(3);
        expect(loops[1]!.startIndex < loops[0]!.endIndex).toBe(true);
        expect(loops[2]!.startIndex < loops[1]!.endIndex).toBe(true);
    });

    it.each([
        // F E Am Dm
        [0, 7, 2, 1, 'Am Dm F E Am Dm F E Am E Am Dm F E Am Dm F E Am'],
        [0, 8, 2, 0, 'Am Dm F E Am Dm F E Am F E Am Dm F E Am Dm F E Am'],
        [5, 7, 3, 1, 'Am Dm F E Am Dm F E Am Dm E Am Dm F E Am Dm F E Am'],
        [5, 0, 3, 2, 'Am Dm F E Am Dm F E Am Dm Am Dm F E Am Dm F E Am'],
        [8, 0, 0, 2, 'Am Dm F E Am Dm F E Am Dm F Am Dm F E Am Dm F E Am'],
        [8, 5, 0, 3, 'Am Dm F E Am Dm F E Am Dm F Dm F E Am Dm F E Am'],
        [7, 5, 1, 3, 'Am Dm F E Am Dm F E Am Dm F E Dm F E Am Dm F E Am'],
        [7, 8, 1, 0, 'Am Dm F E Am Dm F E Am Dm F E F E Am Dm F E Am'],
        [8, 9, 1, 0, 'A#m D#m F# E# A#m D#m F# E# A#m D#m F# E# F# E# A#m D#m F# E# A#m'],
    ])('SelfJumpSimple(%i, %i, %i, %i, %s)', (fromNormalizationRoot, toNormalizationRoot, rootIndex1, rootIndex2, sequenceChords) => {
        const { sequence, firstRoot } = inputChords(sequenceChords);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loopSelfJumpsBlocks).toHaveLength(1);
        const loopSelfMultiJumpBlock = single(loopSelfJumpsBlocks);

        expect(loopSelfMultiJumpBlock.childJumps).toHaveLength(1);
        const loopSelfJumpBlock = single(loopSelfMultiJumpBlock.childJumps);

        expect(loopSelfJumpBlock.jointLoop).not.toBeNull();
        expect(loopSelfJumpBlock.jointMovement).not.toBeNull();
        expect(loopSelfMultiJumpBlock.hasModulations).toBe(false);
        expect(loopSelfMultiJumpBlock.isModulation).toBe(false);
        expect(loopSelfMultiJumpBlock.normalizationRootsFlow).toHaveLength(1);
        expect(loopSelfJumpBlock.loopSelfJumpType).toBe(LoopSelfJumpType.SameKeyJoint);

        assertNormalized({ root1: fromNormalizationRoot, root2: toNormalizationRoot }, { rootIndex1, rootIndex2 }, loopSelfJumpBlock);
    });

    it('SelfJumpMultiModulationOneJointOneAmbiguous', () => {
        //                    1 4 1 4 1               4 1 4 1 4 1 4
        //                            4 1 4 1 4 1 4 1
        const sequenceChords = 'C F C F C G C G C G C G F C F C F C F';
        const { sequence, firstRoot } = inputChords(sequenceChords);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loopSelfJumpsBlocks).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).childJumps.length).toBe(2);
        expect(single(loopSelfJumpsBlocks).childLoops.length).toBe(4);
        expect(single(loopSelfJumpsBlocks).childJumps.every(x => x.isModulation)).toBe(true);
        expect(single(loopSelfJumpsBlocks).hasModulations).toBe(true);
        expect(single(loopSelfJumpsBlocks).isModulation).toBe(false);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow.length).toBe(3);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow[2]).toBe(single(loopSelfJumpsBlocks).normalizationRootsFlow[0]);

        expect(single(loopSelfJumpsBlocks).childJumps[0]!.jointLoop === null).toBe(true);
        expect(single(loopSelfJumpsBlocks).childJumps[0]!.jointMovement === null).toBe(true);
        expect(single(loopSelfJumpsBlocks).childJumps[0]!.loopSelfJumpType).toBe(LoopSelfJumpType.ModulationAmbiguousChord);

        assertNormalized({ root1: 3, root2: 3 }, { rootIndex1: 0, rootIndex2: 1 }, single(loopSelfJumpsBlocks).childJumps[0]!);

        expect(single(loopSelfJumpsBlocks).childJumps[1]!.jointLoop !== null).toBe(true);
        expect(single(loopSelfJumpsBlocks).childJumps[1]!.jointMovement !== null).toBe(true);
        expect(single(loopSelfJumpsBlocks).childJumps[1]!.loopSelfJumpType).toBe(LoopSelfJumpType.ModulationJointMovement);

        assertNormalized({ root1: 10, root2: 8 }, { rootIndex1: 0, rootIndex2: 1 }, single(loopSelfJumpsBlocks).childJumps[1]!);
    });

    it.each([
        //                                                         1 4 5 6 1 4 5 6 1 4
        //                                                                           6 1  4  5  6 1  4  5  6
        [8, 0, 2, false, 'C F G A C F G A C F G# C# D# F G# C# D# F'],

        //                                                         1 4 5 6 1 4 5 6 1 4
        //                                                                           1  4  5 6 1 4  5 6
        [8, 0, 3, true, 'C F G A C F G A C F  A# C D F A# C D'],
    ])('SelfJumpModulationAmbiguousWithAndWithoutJointLoop(%i, %i, %i, %s, %s)', (root, rootIndex1, rootIndex2, hasJointLoop, sequenceChords) => {
        const { sequence, firstRoot } = inputChords(sequenceChords);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loopSelfJumpsBlocks).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).childJumps).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).childLoops.length).toBe(hasJointLoop ? 3 : 2);
        expect(single(loopSelfJumpsBlocks).childJumps.every(x => x.isModulation)).toBe(true);
        expect(single(loopSelfJumpsBlocks).hasModulations).toBe(true);
        expect(single(loopSelfJumpsBlocks).isModulation).toBe(true);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow.length).toBe(2);

        expect(single(loopSelfJumpsBlocks).childJumps[0]!.jointLoop !== null).toBe(hasJointLoop);
        expect(single(loopSelfJumpsBlocks).childJumps[0]!.jointMovement === null).toBe(true);
        expect(single(loopSelfJumpsBlocks).childJumps[0]!.loopSelfJumpType).toBe(LoopSelfJumpType.ModulationAmbiguousChord);

        assertNormalized({ root1: root, root2: root }, { rootIndex1, rootIndex2 }, single(loopSelfJumpsBlocks).childJumps[0]!);
    });

    it('SelfJumpMultiModulationWithJointMovements', () => {
        const sequenceChords = 'C F C F D G D G D G A E A E A E';
        const { sequence, firstRoot } = inputChords(sequenceChords);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loopSelfJumpsBlocks).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).childJumps.length).toBe(2);
        expect(single(loopSelfJumpsBlocks).childLoops.length).toBe(3);
        expect(single(loopSelfJumpsBlocks).childJumps.every(x => x.jointMovement !== null)).toBe(true);
        expect(single(loopSelfJumpsBlocks).childJumps.every(x => x.jointLoop === null)).toBe(true);
        expect(single(loopSelfJumpsBlocks).hasModulations).toBe(true);
        expect(single(loopSelfJumpsBlocks).isModulation).toBe(true);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow.length).toBe(3);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow).toEqual([...new Set(single(loopSelfJumpsBlocks).normalizationRootsFlow)]);
        expect(single(loopSelfJumpsBlocks).childJumps[0]!.loopSelfJumpType).toBe(LoopSelfJumpType.ModulationJointMovement);
        expect(single(loopSelfJumpsBlocks).childJumps[1]!.loopSelfJumpType).toBe(LoopSelfJumpType.ModulationJointMovement);

        assertNormalized({ root1: 8, root2: 5 }, { rootIndex1: 1, rootIndex2: 0 }, single(loopSelfJumpsBlocks).childJumps[0]!);
        assertNormalized({ root1: 10, root2: 0 }, { rootIndex1: 1, rootIndex2: 1 }, single(loopSelfJumpsBlocks).childJumps[1]!);
    });

    it('SelfJumpMulti', () => {
        const sequenceChords = 'Am Dm F E Am Dm F E Am E Am Dm F E Am Dm F E Am E Am Dm F E';
        const { sequence, firstRoot } = inputChords(sequenceChords);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loopSelfJumpsBlocks).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).childJumps.length).toBe(2);
        expect(single(loopSelfJumpsBlocks).childLoops.length).toBe(5);
        expect(single(loopSelfJumpsBlocks).childJumps.every(x => x.jointLoop !== null)).toBe(true);
        expect(single(loopSelfJumpsBlocks).childJumps.every(x => x.jointMovement !== null)).toBe(true);
        expect(single(loopSelfJumpsBlocks).hasModulations).toBe(false);
        expect(single(loopSelfJumpsBlocks).isModulation).toBe(false);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).childJumps.every(x => x.loopSelfJumpType === LoopSelfJumpType.SameKeyJoint)).toBe(true);
    });

    it('SelfJumpSameShift', () => {
        const { sequence, firstRoot } = inputDeltasParams(
            3, 3, 6,
            3, 3, 6,
            3, 9,
            3, 3, 6,
            3, 3, 6);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(3);
        expect(loops[2]!.normalized).toBe(loops[0]!.normalized);
        expect(loops[2]!.normalizationRoot).toBe(loops[0]!.normalizationRoot);
        expect(loops[2]!.normalizationShift).toBe(loops[0]!.normalizationShift);
    });

    it('SelfJumpDifferentShift', () => {
        const { sequence, firstRoot } = inputDeltasParams(
            3, 3, 6,
            3, 3, 6,
            6, 6,
            3, 3, 6,
            3, 3, 6);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(3);
        expect(loops[2]!.normalized).toBe(loops[0]!.normalized);
        expect(loops[2]!.normalizationRoot).toBe(loops[0]!.normalizationRoot);
        expect(loops[2]!.normalizationShift).not.toBe(loops[0]!.normalizationShift);
    });

    it('SelfJumpLong', () => {
        const { sequence, firstRoot } = inputDeltasParams(
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 2, 9,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(3);
        expect(loops[2]!.normalized).toBe(loops[0]!.normalized);
        expect(loops[2]!.normalizationRoot).toBe(loops[0]!.normalizationRoot);
        expect(loops[2]!.normalizationShift).toBe(loops[0]!.normalizationShift);
    });

    it.each([
        [6, 2, 0, 1, 'C C# D# E C C# D# E C C# D# E F# G D# E F# G D# E F# G'],
        [6, 2, 0, 2, 'C C# D# E F# C C# D# E F# C C# D# E F# G A D# E F# G A D# E F# G A'],
        [6, 2, 0, 3, 'C C# D# E F# G C C# D# E F# G C C# D# E F# G A Bb D# E F# G A Bb D# E F# G A Bb'],
    ])('SelfJumpWithModulationWithMultipleOverlaps(%i, %i, %i, %i, %s)', (root, rootIndex1, rootIndex2, overlapLength, sequenceChords) => {
        const { sequence, firstRoot } = inputChords(sequenceChords);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(single(loopSelfJumpsBlocks).childJumps).toHaveLength(1);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow.length).toBe(2);
        expect(single(single(loopSelfJumpsBlocks).childJumps).loopSelfJumpType).toBe(LoopSelfJumpType.ModulationOverlap);

        assertNormalizedOverlaps(root, { rootIndex1, rootIndex2, overlapLength }, single(single(loopSelfJumpsBlocks).childJumps));
    });

    it('SelfJumpWithModulationWithOverlap', () => {
        const { sequence, firstRoot } = inputDeltasParams(1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 2,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(2);
        expect(loops[1]!.normalized).toBe(loops[0]!.normalized);
        expect(loops[1]!.normalizationRoot).not.toBe(loops[0]!.normalizationRoot);
        expect(single(single(loopSelfJumpsBlocks).childJumps).jointLoop).toBeNull();
        expect(single(single(loopSelfJumpsBlocks).childJumps).jointMovement).toBeNull();
        expect(single(loopSelfJumpsBlocks).hasModulations).toBe(true);
        expect(single(loopSelfJumpsBlocks).isModulation).toBe(true);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow.length).toBe(2);
        expect(single(single(loopSelfJumpsBlocks).childJumps).loopSelfJumpType).toBe(LoopSelfJumpType.ModulationOverlap);
    });

    it('SelfJumpWithModulationWithJoint', () => {
        const { sequence, firstRoot } = inputDeltasParams(
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 10,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6);

        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        expect(loops.length).toBe(3);
        expect(loops[2]!.normalized).toBe(loops[0]!.normalized);
        expect(loops[2]!.normalizationRoot).not.toBe(loops[0]!.normalizationRoot);
        expect(single(single(loopSelfJumpsBlocks).childJumps).jointLoop).not.toBeNull();
        expect(single(single(loopSelfJumpsBlocks).childJumps).jointMovement).not.toBeNull();
        expect(single(loopSelfJumpsBlocks).hasModulations).toBe(true);
        expect(single(loopSelfJumpsBlocks).isModulation).toBe(true);
        expect(single(loopSelfJumpsBlocks).normalizationRootsFlow.length).toBe(2);
        expect(single(single(loopSelfJumpsBlocks).childJumps).loopSelfJumpType).toBe(LoopSelfJumpType.ModulationJointMovement);
    });

    it('SelfJumpWithModulationWithMovement', () => {
        for (const chords of [
            'C D E F C D E F C D E F F# G# A# B F# G# A# B F# G# A# B',
            'C D E F C D E F C D E F G# A# B F# G# A# B F# G# A# B',
            'C D E F C D E F C D E F A# B F# G# A# B F# G# A# B',
            'C D E F C D E F C D E F B F# G# A# B F# G# A# B',
        ]) {
            const { sequence, firstRoot } = inputChords(chords);

            const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
            const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
            traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

            expect(loops.length).toBe(2);
            expect(loops[1]!.normalized).toBe(loops[0]!.normalized);
            expect(loops[1]!.normalizationRoot).not.toBe(loops[0]!.normalizationRoot);
            expect(((x: LoopSelfJumpBlock) => x.jointLoop === null && x.jointMovement !== null)(single(single(loopSelfJumpsBlocks).childJumps))).toBe(true);
            expect(single(loopSelfJumpsBlocks).hasModulations).toBe(true);
            expect(single(loopSelfJumpsBlocks).isModulation).toBe(true);
            expect(single(loopSelfJumpsBlocks).normalizationRootsFlow.length).toBe(2);
            expect(single(single(loopSelfJumpsBlocks).childJumps).loopSelfJumpType).toBe(LoopSelfJumpType.ModulationJointMovement);
        }
    });

    it('Random100X100WithChordTypes', () => {
        for (let i = 0; i < 100; i++) {
            const getRandomChordType = (): ChordType => next(ChordType.Major, ChordType.Augmented + 1) as ChordType;
            let chordType = getRandomChordType();
            const sequence = range(0, 100).map(() => next(0, 12)).map(
                d => new CompactHarmonyMovement({
                    rootDelta: d,
                    fromType: chordType,
                    toType: chordType = getRandomChordType(),
                })).filter(m => m.fromType !== m.toType || m.rootDelta !== 0);

            const firstRoot = next(0, 12);
            const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
            const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
            traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, false);
        }
    }, 600000);

    it.each([
        [0, 8, 'A# C F G Am C F G Am F G Am C F G Am C D'],
        [0, 10, 'A# C F G Am C F G Am G Am C F G Am C D'],
        [0, 7, 'Am Dm F E Am Dm F E Am E Am Dm F E Am Dm F E'],
    ])('JointLoopIndicesCheck(%i, %i, %s)', (fromNormalizationRoot, toNormalizationRoot, inputChordsText) => {
        const { sequence, firstRoot } = inputChords(inputChordsText);
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        const loopSelfMultiJumpBlock = single(loopSelfJumpsBlocks);
        const loopSelfJumpBlock = single(loopSelfMultiJumpBlock.childJumps);

        expect(toNormalizedRoots(asRootIndices(loopSelfJumpBlock.jumpPoints!), loopSelfJumpBlock.normalized, loopSelfJumpBlock.loop1.normalizationRoot))
            .toEqual({ root1: fromNormalizationRoot, root2: toNormalizationRoot });
    });

    it('MassiveOverlapsCheck', () => {
        const { sequence, firstRoot } = inputChords('A F A B C D A Bm B C Cm D Dm A F D Dm A F');
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfJumpsBlocks = indexExtractor.findSelfJumps(sequence, loops);
        traceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        const massiveOverlaps = indexExtractor.findMassiveOverlapsBlocks(loops);

        expect(massiveOverlaps).toHaveLength(1);

        expect(single(massiveOverlaps).internalLoops.length).toBe(2);
    });

    it('MultipleJumpsBlocks', () => {
        const { sequence, firstRoot } = inputChords('Am C D Am C D Am D Am C D E C D E C E C D E C D E');
        const loops = indexExtractor.findLoops(sequence, indexExtractor.createRoots(sequence, firstRoot));
        const loopSelfMultiJumpBlocks = indexExtractor.findSelfJumps(sequence, loops);
        expect(loopSelfMultiJumpBlocks.length).toBe(2);
    });
});

interface RootIndices {
    readonly rootIndex1: number;
    readonly rootIndex2: number;
}

/** C#'s implicit conversion of JumpPoints, (root1Index, root2Index), to (rootIndex1, rootIndex2). */
function asRootIndices(jumpPoints: { readonly root1Index: number; readonly root2Index: number }): RootIndices {
    return { rootIndex1: jumpPoints.root1Index, rootIndex2: jumpPoints.root2Index };
}

function toNormalizedRoots(rootIndices: RootIndices, normalized: string,
    normalizationRoot1: number, normalizationRoot2: number | null = null): { root1: number; root2: number } {
    const s = deserializeLoop(normalized);
    const roots = indexExtractor.createRoots(s, normalizationRoot1);
    const { roots1, roots2 } = normalizationRoot2 === null
        ? { roots1: roots, roots2: roots }
        : { roots1: roots, roots2: indexExtractor.createRoots(s, normalizationRoot2) };
    return { root1: roots1[rootIndices.rootIndex1]!, root2: roots2[rootIndices.rootIndex2]! };
}

function traceAndTest(
    sequence: Sequence,
    firstRoot: number,
    loops: readonly LoopBlock[],
    loopSelfJumpsBlocks: readonly LoopSelfMultiJumpBlock[],
    trace = true,
    visualize = true): void {
    const massiveOverlaps = indexExtractor.findMassiveOverlapsBlocks(loops);
    const anyMassiveOverlaps = massiveOverlaps.length > 0;

    massiveOverlaps.forEach(o => expect(o.internalLoops).not.toContain(o.edge1));
    massiveOverlaps.forEach(o => expect(o.internalLoops).not.toContain(o.edge2));

    assertLoopsSequencePossible(loops, anyMassiveOverlaps);
    assertLoopsSequencePossible(loopSelfJumpsBlocks, false);

    const roots = indexExtractor.createRoots(sequence, firstRoot);

    traceAndTestBlocks(indexExtractor.findBlocks(sequence, roots, BlockTypes.Loop), anyMassiveOverlaps);

    traceAndTestGraph(indexExtractor.createGraph(indexExtractor.findBlocks(sequence, roots, BlockTypes.Loop)));

    const all = indexExtractor.findBlocks(sequence, roots, BlockTypes.All);
    const graph = indexExtractor.createGraph(all);
    const shortestPath = pathsSearcher.dijkstra(graph);
    traceAndTestGraph(graph);

    // sequence integrity test
    expect(asPairs(sequence).every(p => p.current.fromType === p.previous.toType), 'sequential to type and from type mismatch').toBe(true);
    sequence.forEach(m => expect(m.rootDelta === 0 && m.fromType === m.toType, 'null movements in the sequence').toBe(false));

    expect(anyDuplicates(loops.map(l => l.startIndex))).toBe(false);
    expect(anyDuplicates(loops.map(l => l.endIndex))).toBe(false);
    expect(anyDuplicates(loopSelfJumpsBlocks.map(l => l.startIndex))).toBe(false);
    expect(anyDuplicates(loopSelfJumpsBlocks.map(l => l.endIndex))).toBe(false);

    loops.forEach(l => expect(l.loopLength > 1).toBe(true));

    // Math.Round(float) rounds the double half to even; equality with it is a test for being whole either way.
    const isWhole = (x: number) => x === Math.round(x);

    loops.forEach(l => expect(isWhole(l.occurrences)).toBe(isWhole(l.successions)));

    loops.forEach(l => expect(l.occurrences >= 1).toBe(true));
    loops.forEach(l => expect(l.successions >= 0).toBe(true));

    loops.forEach(l => expect(
        isWhole(l.occurrences)
        && isWhole(l.eachChordCoveredTimes)).toBe(false));

    loops.forEach(l => expect(l.eachChordCoveredTimes > toFloat(1 + floatEpsilon)).toBe(true));

    loops.forEach(l => expect(
        roots[l.startIndex + l.loopLength - 1] === roots[l.endIndex + 1] && sequence[l.startIndex + l.loopLength - 1]!.fromType === sequence[l.endIndex]!.toType).toBe(
        isWhole(l.eachChordCoveredTimes)));

    loops.forEach(l => expect(
        l.eachChordCoveredTimesWhole).toBe(
        isWhole(l.eachChordCoveredTimes)));

    loops.forEach(l => expect(
        roots[l.startIndex] === roots[l.endIndex + 1] && sequence[l.startIndex]!.fromType === sequence[l.endIndex]!.toType).toBe(
        isWhole(l.successions)));

    loops.forEach(l => expect(
        l.occurrencesWhole).toBe(
        isWhole(l.successions)));

    loops.forEach(l => expect(
        l.successionsSignificant).toBe(
        l.successions >= 1));

    loops.forEach(l => expect(
        l.eachChordCoveredTimesSignificant).toBe(
        l.eachChordCoveredTimes >= 2));

    loops.forEach(l => expect(
        toFloat(l.occurrences - 1)).toBe(
        l.successions));

    const createRootsTrace = (block: IBlock) => progressionsVisualizer.createRootsTraceByIndices(sequence, roots, new Out<number[]>(), true, block);

    if (trace) {
        logger.logInformation(
            `loops: ${loops.length}, self jumps: ${loopSelfJumpsBlocks.length}, roots: ${visualize ? '' : progressionsVisualizer.createRootsTraceByIndices(sequence, roots, new Out<number[]>())}`);

        if (visualize) {
            const result = progressionsVisualizer.visualizeBlocksAsOne(sequence, roots, all, graph, shortestPath, new BlocksChartParameters({ typesToo: false }));
            logger.logInformation('\n' + result + '\n');
        }
    }

    // blocks sequential loops test
    expect(loopSelfJumpsBlocks.every(x => asPairs(x.childJumps).every(p => p.current.loop1 === p.previous.loop2))).toBe(true);

    for (const loopSelfJumpsBlock of loopSelfJumpsBlocks) {
        // has modulations integrity
        expect(loopSelfJumpsBlock.childJumps.some(x => x.isModulation)).toBe(loopSelfJumpsBlock.hasModulations);
        expect(!loopSelfJumpsBlock.isModulation || loopSelfJumpsBlock.hasModulations).toBe(true); // IsModulation => HasModulation

        // normalization roots flow
        expect(asPairs(loopSelfJumpsBlock.normalizationRootsFlow).every(p => p.current !== p.previous)).toBe(true);
        expect(loopSelfJumpsBlock.normalizationRootsFlow.length > 1).toBe(loopSelfJumpsBlock.hasModulations);
        expect(loopSelfJumpsBlock.normalizationRootsFlow[0] !== loopSelfJumpsBlock.normalizationRootsFlow[loopSelfJumpsBlock.normalizationRootsFlow.length - 1]).toBe(loopSelfJumpsBlock.isModulation);

        // child self jumps are linked
        expect(asPairs(loopSelfJumpsBlock.childJumps).every(x => x.current.loop1 === x.previous.loop2)).toBe(true);

        // child loops are the same as child jumps
        expectSameSequence(
            loopSelfJumpsBlock.childLoops,
            loopSelfJumpsBlock.childJumps
                .flatMap((x, i) => i === 0 ? [x.loop1, x.jointLoop, x.loop2] : [x.jointLoop, x.loop2])
                .filter(x => x !== null));

        const endMovement = loopSelfJumpsBlock.startIndex + loopSelfJumpsBlock.loopLength - 1;

        const normalized = deserializeLoop(loopSelfJumpsBlock.normalized);

        const childJumps = loopSelfJumpsBlock.childJumps.map(j => {
            const gap = j.loop2.startIndex - j.loop1.endIndex;
            expect(gap === 1 || gap === 2 || gap <= 0).toBe(true);

            let jumpPoints: string | null = null;
            if (j.jumpPoints !== null) {
                const normalizedRoots1 = indexExtractor.createRoots(normalized, j.loop1.normalizationRoot);
                const normalizedRoots2 = indexExtractor.createRoots(normalized, j.loop2.normalizationRoot);
                jumpPoints = `[${tupleToString(j.jumpPoints)}] ${normalizedRoots1[j.jumpPoints.root1Index]} -> ${normalizedRoots2[j.jumpPoints.root2Index]}`;

                switch (j.loopSelfJumpType) {
                    case LoopSelfJumpType.ModulationJointMovement:
                        break;
                    case LoopSelfJumpType.SameKeyJoint:
                        expect(j.loop2.normalizationRoot).toBe(j.loop1.normalizationRoot);
                        break;
                    case LoopSelfJumpType.ModulationAmbiguousChord:
                        expect(normalizedRoots2[j.jumpPoints.root2Index]).toBe(normalizedRoots1[j.jumpPoints.root1Index]);
                        break;
                }
            }

            if (j.overlapJumpPoints !== null) {
                const normalizedRoots1 = indexExtractor.createRoots(normalized, j.loop1.normalizationRoot);
                const normalizedRoots2 = indexExtractor.createRoots(normalized, j.loop2.normalizationRoot);
                expect(normalizedRoots2[j.overlapJumpPoints.overlapStartRoot2Index]).toBe(normalizedRoots1[j.overlapJumpPoints.overlapStartRoot1Index]);
                jumpPoints = `[${tupleToString(j.overlapJumpPoints)}] ${normalizedRoots1[j.overlapJumpPoints.overlapStartRoot1Index]} -> ${normalizedRoots2[j.overlapJumpPoints.overlapStartRoot2Index]}`;
            }

            const jumpEndMovement = j.startIndex + j.loopLength - 1;
            return `SJ ${j.loopLength}` +
                ` + ${j.endIndex - jumpEndMovement}` +
                ` (${j.startIndex}..${jumpEndMovement}..${j.endIndex}) ${LoopSelfJumpType[j.loopSelfJumpType]}:` +
                ` (jump points = ${jumpPoints ?? ''})` +
                ` (keys = ${j.loop1.normalizationRoot} => ${j.loop2.normalizationRoot}, c = ${j.modulationDelta})` +
                ` (is-modulation=${j.isModulation ? 'True' : 'False'})` +
                ` (joint-loop=${j.jointLoop !== null ? 'True' : 'False'})` +
                ` (joint-movement=${j.jointMovement !== null ? 'True' : 'False'})` +
                ` roots: ${createRootsTrace(j)}`;
        }).join('\n');

        if (trace) {
            logger.logInformation(`SJS ${loopSelfJumpsBlock.loopLength}` +
                ` + ${loopSelfJumpsBlock.endIndex - endMovement}` +
                ` (${loopSelfJumpsBlock.startIndex}..${endMovement}..${loopSelfJumpsBlock.endIndex}):` +
                ` (keys=${loopSelfJumpsBlock.normalizationRootsFlow.join(', ')})` +
                ` (has-modulations=${loopSelfJumpsBlock.hasModulations ? 'True' : 'False'})` +
                `\nall roots: ${createRootsTrace(loopSelfJumpsBlock)};` +
                `\njumps:\n${childJumps}`);
        }
    }

    for (const loopSelfJumpBlock of loopSelfJumpsBlocks.flatMap(x => x.childJumps)) {
        // key change
        expect(loopSelfJumpBlock.modulationDelta === 0).toBe(!loopSelfJumpBlock.isModulation);
        expect(loopSelfJumpBlock.modulationDelta >= 0 && loopSelfJumpBlock.modulationDelta <= 11).toBe(true);

        // from and to are not equal and not sequential for non-modulated jumps
        if (loopSelfJumpBlock.loopSelfJumpType === LoopSelfJumpType.SameKeyJoint) {
            expect(loopSelfJumpBlock.overlapJumpPoints).toBeNull();
            expect(loopSelfJumpBlock.jumpPoints).not.toBeNull();
            const from = loopSelfJumpBlock.jumpPoints!.root1Index;
            const to = loopSelfJumpBlock.jumpPoints!.root2Index;
            expect(to).not.toBe(from);
            expect(to).not.toBe((from + 1) % loopSelfJumpBlock.loopLength);
        } else if (loopSelfJumpBlock.loopSelfJumpType === LoopSelfJumpType.ModulationOverlap) {
            expect(loopSelfJumpBlock.overlapJumpPoints).not.toBeNull();
            expect(loopSelfJumpBlock.jumpPoints).toBeNull();
        } else {
            expect(loopSelfJumpBlock.overlapJumpPoints).toBeNull();
        }

        // the joint movement null reason and the loops gap need to correspond
        const loopsGap = loopSelfJumpBlock.loop2.startIndex - loopSelfJumpBlock.loop1.endIndex;
        if (loopSelfJumpBlock.jointMovement === null) {
            if (loopsGap === 1 && loopSelfJumpBlock.loopSelfJumpType === LoopSelfJumpType.ModulationAmbiguousChord) {
                // ok
            } else if (loopsGap < 1 && loopSelfJumpBlock.loopSelfJumpType === LoopSelfJumpType.ModulationOverlap) {
                // ok
            } else {
                expect.fail('The joint movement null reason and the loops gap do not correspond.');
            }
        }

        // end and start with the same loop and the jumps block normalized is the same
        expect(loopSelfJumpBlock.loop2.normalized).toBe(loopSelfJumpBlock.loop1.normalized);
        expect(loopSelfJumpBlock.normalized).toBe(loopSelfJumpBlock.loop1.normalized);

        // the middle loop is not the same
        expect(loopSelfJumpBlock.normalized).not.toBe(loopSelfJumpBlock.jointLoop?.normalized ?? null);

        // the middle loop is inside the outer loops
        if (loopSelfJumpBlock.jointLoop !== null) {
            expect(loopSelfJumpBlock.jointLoop.startIndex > loopSelfJumpBlock.loop1.startIndex).toBe(true);
            expect(loopSelfJumpBlock.jointLoop.endIndex < loopSelfJumpBlock.loop2.endIndex).toBe(true);
        }

        switch (loopSelfJumpBlock.loopSelfJumpType) {
            case LoopSelfJumpType.SameKeyJoint:
                // exactly one extra movement
                expect(loopSelfJumpBlock.loop2.startIndex).toBe(loopSelfJumpBlock.loop1.endIndex + 2);
                expect(loopSelfJumpBlock.jointLoop).not.toBeNull();
                expect(loopSelfJumpBlock.jointMovement).not.toBeNull();
                expect(loopSelfJumpBlock.isModulation).toBe(false);
                break;

            case LoopSelfJumpType.ModulationJointMovement:
                // exactly one extra movement
                expect(loopSelfJumpBlock.loop2.startIndex).toBe(loopSelfJumpBlock.loop1.endIndex + 2);
                expect(loopSelfJumpBlock.jointMovement).not.toBeNull();
                expect(loopSelfJumpBlock.isModulation).toBe(true);
                break;
            case LoopSelfJumpType.ModulationAmbiguousChord:
                // exactly zero extra movements
                expect(loopSelfJumpBlock.loop2.startIndex).toBe(loopSelfJumpBlock.loop1.endIndex + 1);
                expect(loopSelfJumpBlock.jointMovement).toBeNull();
                expect(loopSelfJumpBlock.isModulation).toBe(true);
                break;
            case LoopSelfJumpType.ModulationOverlap:
                expect(loopSelfJumpBlock.loop1.endIndex + 1 > loopSelfJumpBlock.loop2.startIndex).toBe(true);
                expect(loopSelfJumpBlock.loop1.endIndex + 1 - loopSelfJumpBlock.loop2.startIndex).toBe(loopSelfJumpBlock.overlapJumpPoints!.overlapLength);
                expect(loopSelfJumpBlock.jointMovement).toBeNull();
                expect(loopSelfJumpBlock.isModulation).toBe(true);
                break;
            default:
                throw new Error('ArgumentOutOfRangeException');
        }
    }

    for (const loop of loops) {
        const endLoopIndex = loop.startIndex + loop.loopLength - 1;

        expect(endLoopIndex - loop.startIndex + 1).toBe(loop.loopLength);
        expect(endLoopIndex <= loop.endIndex).toBe(true);
        expect(loop.startIndex >= 0 && loop.startIndex <= sequence.length - 1).toBe(true);
        expect(endLoopIndex >= 0 && endLoopIndex <= sequence.length - 1).toBe(true);
        expect(loop.endIndex >= 0 && loop.endIndex <= sequence.length - 1).toBe(true);

        // loop normalization shift correctly set value test
        const shift = new Out<number>();
        const actualNormalized = serializeLoop(indexExtractor.getNormalizedProgression(loop.loop, shift, new Out<number>()));
        expect([actualNormalized, shift.value]).toEqual([loop.normalized, loop.normalizationShift]);

        const rootsTrace = createRootsTrace(loop);

        if (trace) {
            logger.logInformation(`L ${loop.loopLength}` +
                ` + ${loop.endIndex - endLoopIndex}` +
                ` (${loop.startIndex}..${endLoopIndex}..${loop.endIndex}):` +
                ` ${rootsTrace};`);
        }

        const normalized = deserializeLoop(loop.normalized);
        const normalizedRoots = indexExtractor.createRoots(normalized, loop.normalizationRoot);
        const normalizedRootsIndices = [-1 + normalized.length, ...range(0, loop.endIndex - loop.startIndex + 1)]
            .map(x => x + loop.normalizationShift)
            .map(i => (i % normalized.length) + 1);
        const normalizedRecreation = normalizedRootsIndices
            .map(i => `${new Note(normalizedRoots[i]!).representation(new RepresentationSettings())}${chordTypeToString(i === 0 ? normalized[0]!.fromType : normalized[i - 1]!.toType)}`)
            .join(' ');

        // loop normalized recreation test and the normalization start root test
        expect(normalizedRecreation).toBe(rootsTrace);
        expect(loop.normalizationRoot).toBe(normalizedRoots[0]);

        // loop type cycle test
        expect(loop.loop[loop.loop.length - 1]!.toType).toBe(loop.loop[0]!.fromType);

        // loop lower boundary test
        const rootIndexBeforeLoop = loop.startIndex - 1;
        if (rootIndexBeforeLoop >= 0) {
            expect([roots[rootIndexBeforeLoop], sequence[loop.startIndex - 1]!.fromType]).not.toEqual(
                [roots[rootIndexBeforeLoop + loop.loopLength], sequence[endLoopIndex]!.fromType]);
        }

        // loop upper boundary test
        if (loop.endIndex + 1 < sequence.length) {
            expect([roots[loop.endIndex + 2], sequence[loop.endIndex + 1]!.toType]).not.toEqual(
                [roots[loop.endIndex - loop.loopLength + 2], sequence[loop.endIndex - loop.loopLength + 1]!.toType]);
        }
    }
}

function traceAndTestGraph(graph: BlockGraph): void {
    [...graph.environments.values()].forEach(e => expectDistinct(e.childrenSubtree));
    [...graph.environments.values()].forEach(e => expectDistinct(e.children));
    [...graph.environments.values()].forEach(e => expectDistinct(e.parents));
    [...graph.environments.values()].forEach(e => expectDistinct(e.leftJoints));
    [...graph.environments.values()].forEach(e => expectDistinct(e.rightJoints));
}

function traceAndTestBlocks(blocks: readonly IBlock[], anyMassiveOverlaps: boolean): void {
    assertLoopsSequencePossible(blocks, anyMassiveOverlaps);

    // sequences do not overlap with anything else
    expect(blocks.filter((x): x is SequenceBlock => x instanceof SequenceBlock).some(s => blocks.filter(b => !(b instanceof PingPongBlock)).some(l => l !== s && (
        l.startIndex >= s.startIndex && l.startIndex <= s.endIndex // l.StartIndex within the sequence
        || l.endIndex >= s.startIndex && l.endIndex <= s.endIndex)))).toBe(false); // l.EndIndex within the sequence
}

function assertLoopsSequencePossible(blocksSource: readonly IBlock[], anyMassiveOverlaps: boolean): void {
    const blocks = blocksSource.filter(x => !(x instanceof PingPongBlock));

    // there's no such block that contains another block
    expect(blocks.some(x => blocks.filter(y => y !== x)
        .some(y => x.startIndex <= y.startIndex && x.endIndex >= y.endIndex))).toBe(false);

    // any movement is covered by a maximum of 2 multijumps
    expect(groupBy(blocks
        .flatMap(x => range(x.startIndex, x.endIndex - x.startIndex + 1).map(i => ({ i, isLoop: x instanceof LoopBlock }))), x => x.i)
        .some(x => anyMassiveOverlaps

            // if multiple simple blocks, treat them as one
            ? x.items.length
              - x.items.filter(x => x.isLoop).length
              + (x.items.some(x => x.isLoop) ? 1 : 0)
              > 2

            // no massive overlaps, not more than two are allowed, even simple
            : x.items.length > 2)).toBe(false);

    if (anyMassiveOverlaps) {
        expect(groupBy(blocks
            .flatMap(x => range(x.startIndex, x.endIndex - x.startIndex + 1).map(i => ({ i, isLoop: x instanceof LoopBlock }))), x => x.i)
            .some(x => x.items.length > 2)).toBe(true);
    }
}

function assertNormalized(roots: { readonly root1: number; readonly root2: number }, rootIndices: RootIndices, loopSelfJumpBlock: LoopSelfJumpBlock): void {
    expectTupleEqual(roots, toNormalizedRoots(
        asRootIndices(loopSelfJumpBlock.jumpPoints!),
        loopSelfJumpBlock.normalized,
        loopSelfJumpBlock.loop1.normalizationRoot,
        loopSelfJumpBlock.loop2.normalizationRoot));
    expectTupleEqual(rootIndices, loopSelfJumpBlock.jumpPoints);
}

function assertNormalizedOverlaps(root: number, rootIndices: { readonly rootIndex1: number; readonly rootIndex2: number; readonly overlapLength: number }, loopSelfJumpBlock: LoopSelfJumpBlock): void {
    expectTupleEqual({ root1: root, root2: root }, toNormalizedRoots(
        { rootIndex1: loopSelfJumpBlock.overlapJumpPoints!.overlapStartRoot1Index, rootIndex2: loopSelfJumpBlock.overlapJumpPoints!.overlapStartRoot2Index },
        loopSelfJumpBlock.normalized,
        loopSelfJumpBlock.loop1.normalizationRoot,
        loopSelfJumpBlock.loop2.normalizationRoot));
    expectTupleEqual(rootIndices, loopSelfJumpBlock.overlapJumpPoints);
}

/** InputDeltas(params int[] deltas) => InputDeltas(deltas, 0). */
function inputDeltasParams(...deltas: number[]): { sequence: Sequence; firstRoot: number } {
    return inputDeltas(deltas, 0);
}

/** InputDeltas(IEnumerable<int> deltas, byte? constRoot = null). */
function inputDeltas(deltas: readonly number[], constRoot: number | null = null): { sequence: Sequence; firstRoot: number } {
    const firstRoot = constRoot ?? next(1, 12);
    const sequence = deltas.map(d => new CompactHarmonyMovement({
        rootDelta: d & 0xff,
        toType: ChordType.Augmented,
        fromType: ChordType.Augmented,
    }));

    return { sequence, firstRoot };
}

function inputChords(chords: string): { sequence: Sequence; firstRoot: number } {
    const progression = progressionsBuilder.buildProgression(split(chords, [' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .map(x => {
            let note = Note.CharactersToNotes.get(x[0]!);
            if (note === undefined) throw new Error('KeyNotFoundException');
            x = x.substring(1);
            if (x.length > 0 && (x[0] === '#' || x[0] === 'b')) {
                switch (x[0]) {
                    case '#':
                        note = note.sharp();
                        break;
                    case 'b':
                        note = note.flat();
                        break;
                }

                x = x.substring(1);
            }

            return chordDataParser.getProgressionData(
                `[!${note.value}!]${x}`);
        }));

    return { sequence: single(progression.extendedHarmonyMovementsSequences).movements.map(x => x.compact()), firstRoot: single(progression.extendedHarmonyMovementsSequences).firstRoot };
}
