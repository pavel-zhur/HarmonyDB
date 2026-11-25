import { Dictionary, Lookup } from '../../common/dotnet/Collections';
import { ArgumentException, ArgumentOutOfRangeException, KeyNotFoundException } from '../../common/dotnet/exceptions';
import { at, atFromEnd, compareBooleans, compareStrings, distinct, except, first, groupBy, maxBy, maxInt, minBy, minInt, orderBy, range, sumInt, spanAt } from '../../common/dotnet/Linq';
import { listSort } from '../../common/dotnet/ListSort';
import { compareInts, intMod, toFloat, toShort } from '../../common/dotnet/Numbers';
import { Out } from '../../common/dotnet/Out';
import { anyDuplicates, asPairs, once, toChunks, toChunksByShouldStartNew, withIndices, withPrevious } from '../../common/OneShelf.Common/Extensions';
import { Note } from '../../representations';
import type { ChordType } from '../Models/ChordType';
import type { CompactChordsProgression } from '../Models/CompactV1/CompactChordsProgression';
import type { CompactHarmonyMovement } from '../Models/CompactV1/CompactHarmonyMovement';
import { EdgeBlock } from '../Models/Index/Blocks/EdgeBlock';
import type { IBlock } from '../Models/Index/Blocks/Interfaces/IBlock';
import type { IIndexedBlock } from '../Models/Index/Blocks/Interfaces/IIndexedBlock';
import { isIIndexedBlock } from '../Models/Index/Blocks/Interfaces/IIndexedBlock';
import type { IPolyBlock } from '../Models/Index/Blocks/Interfaces/IPolyBlock';
import { isIPolyBlock } from '../Models/Index/Blocks/Interfaces/IPolyBlock';
import { LoopBlock } from '../Models/Index/Blocks/LoopBlock';
import { LoopMassiveOverlapsBlock } from '../Models/Index/Blocks/LoopMassiveOverlapsBlock';
import { LoopSelfJumpBlock } from '../Models/Index/Blocks/LoopSelfJumpBlock';
import { LoopSelfMultiJumpBlock } from '../Models/Index/Blocks/LoopSelfMultiJumpBlock';
import { PingPongBlock } from '../Models/Index/Blocks/PingPongBlock';
import { PolyLoopBlock } from '../Models/Index/Blocks/PolyLoopBlock';
import { PolySequenceBlock } from '../Models/Index/Blocks/PolySequenceBlock';
import { RoundRobinBlock } from '../Models/Index/Blocks/RoundRobinBlock';
import { SequenceBlock } from '../Models/Index/Blocks/SequenceBlock';
import { BlockDetections } from '../Models/Index/Enums/BlockDetections';
import { BlockType } from '../Models/Index/Enums/BlockType';
import { BlockEnvironment } from '../Models/Index/Graphs/BlockEnvironment';
import { BlockGraph } from '../Models/Index/Graphs/BlockGraph';
import { BlockJoint } from '../Models/Index/Graphs/BlockJoint';
import type { IBlockEnvironment } from '../Models/Index/Graphs/Interfaces/IBlockEnvironment';
import type { IBlockJoint } from '../Models/Index/Graphs/Interfaces/IBlockJoint';
import { PolyBlocksExtractionParameters } from '../Models/Index/PolyBlocksExtractionParameters';
import { StructureLink } from '../Models/Structure/StructureLink';
import { isBetween } from '../Tools/Extensions';
import { serializeLoop } from '../Tools/LoopSerializationExtensions';
import type { PathsSearcher } from './PathsSearcher';

/** (int StartIndex, int EndIndex) */
interface Position {
    readonly startIndex: number;
    readonly endIndex: number;
}

/** (int length, int sublength) */
interface Lengths {
    readonly length: number;
    readonly sublength: number;
}

/** (byte root, ChordType chordType) */
interface ChordKey {
    readonly root: number;
    readonly chordType: ChordType;
}

function getOrThrow<K, V>(dictionary: ReadonlyMap<K, V>, key: K): V {
    if (!dictionary.has(key)) throw new KeyNotFoundException(key);
    return dictionary.get(key)!;
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

/** array[index] = value, throwing as the array indexer does. */
function setAt<T>(array: T[], index: number, value: T): void {
    at(array, index);
    array[index] = value;
}

/** List<T>.RemoveAt. */
function removeAt<T>(list: T[], index: number): void {
    at(list, index);
    list.splice(index, 1);
}

/** List<T>.Remove: the first element equal (here: the same reference) to the item. */
function remove<T>(list: T[], item: T): boolean {
    const index = list.indexOf(item);
    if (index < 0) return false;
    list.splice(index, 1);
    return true;
}

/**
 * C# `out` parameters are Out<T> holders; GetNormalizedProgression's three overloads (none, one or two `out`s) are one
 * method with optional holders.
 */
export class IndexExtractor {
    constructor(private readonly pathsSearcher: PathsSearcher) {}

    findPolyBlocks(
        sequence: readonly CompactHarmonyMovement[],
        roots: readonly number[],
        loops: readonly LoopBlock[],
        extractPolySequences: boolean,
        extractPolyLoops: boolean,
        extractionParameters: PolyBlocksExtractionParameters): { polySequences: PolySequenceBlock[]; polyLoops: PolyLoopBlock[] } {
        if (!extractPolySequences && !extractPolyLoops) throw new ArgumentException();

        const successiveBreaksForSequences: boolean[] = new Array<boolean>(sequence.length + 1).fill(false);
        if (extractionParameters.sequencesExcludeIfContainsSuccessiveLoopsBreak) {
            for (const i of loops
                .filter(x => x.successionsSignificant)
                .flatMap(l => range(l.startIndex + l.loopLength, l.endIndex - l.startIndex - 2 * l.loopLength + 2))) {
                setAt(successiveBreaksForSequences, i, true);
            }
        }

        const successiveBreaksForLoops: { startIndex: number; endIndex: number }[] = [];
        if (extractionParameters.loopsExcludeIfContainsSuccessiveLoopsBreak) {
            for (const loop of loops
                .filter(x => x.successionsSignificant)) {
                if (loop.successions === 2) {
                    successiveBreaksForLoops.push({ startIndex: loop.startIndex, endIndex: loop.endIndex });
                } else {
                    successiveBreaksForLoops.push({ startIndex: loop.startIndex, endIndex: loop.startIndex + loop.loopLength - 1 });
                    successiveBreaksForLoops.push({ startIndex: loop.endIndex - loop.loopLength + 1, endIndex: loop.endIndex });
                }
            }
        }

        const sequenceLength = sequence.length;
        let subsequencePositions = new Map<string, Position[]>();
        const subsequenceNormalizations = new Dictionary<Position, string>();
        const subsequenceOverLoops = new Map<string, Lengths>();
        const subsequenceOverLoopsWithTails = new Map<string, Lengths>();
        const subsequenceLoops = new Set<string>();

        // Identify all possible subsequences
        for (let length = 1; length <= sequenceLength; length++) {
            for (let startIndex = 0; startIndex <= sequenceLength - length; startIndex++) {
                const endIndex = startIndex + length - 1;
                const subsequence = sequence.slice(startIndex, startIndex + length);
                const normalized = serializeLoop(subsequence);
                const position: Position = { startIndex, endIndex };

                // find perfect loops (A, A, ... A), with and without tails
                for (let subLength = 2; subLength < length; subLength++) {
                    if (at(roots, startIndex) !== at(roots, startIndex + subLength)
                        || spanAt(sequence, startIndex).fromType !== spanAt(sequence, startIndex + subLength - 1).toType) {
                        continue;
                    }

                    if (distinct(range(0, Math.trunc(length / subLength))
                            .map(i => at(roots, startIndex + i * subLength)))
                            .length !== 1
                        || distinct(range(0, Math.trunc(length / subLength))
                            .map(i => subsequenceNormalizations.get({ startIndex: startIndex + i * subLength, endIndex: startIndex + (i + 1) * subLength - 1 })))
                            .length !== 1) {
                        continue;
                    }

                    const modulus = intMod(length, subLength);

                    if (modulus === 0) {
                        subsequenceOverLoops.set(normalized, { length, sublength: subLength });
                        break;
                    }

                    if (at(roots, startIndex) === at(roots, endIndex - modulus + 1)
                        && subsequenceNormalizations.get({ startIndex, endIndex: startIndex + modulus - 1 }) === subsequenceNormalizations.get({ startIndex: endIndex - modulus + 1, endIndex })) {
                        subsequenceOverLoopsWithTails.set(normalized, { length, sublength: subLength });
                        break;
                    }
                }

                // find loops
                if (at(roots, position.startIndex) === at(roots, position.endIndex + 1) && spanAt(sequence, position.startIndex).fromType === spanAt(sequence, position.endIndex).toType) {
                    subsequenceLoops.add(normalized);
                }

                if (!subsequencePositions.has(normalized)) {
                    subsequencePositions.set(normalized, []);
                }

                subsequencePositions.get(normalized)!.push(position);

                subsequenceNormalizations.set(position, normalized);
            }
        }

        // Find poly loops
        interface Candidate {
            readonly n: string;
            readonly p: Position;
            readonly length: number;
            readonly sublength: number;
        }

        const loopNormalizations = new Map<string, { readonly normalized: string; readonly normalizationShift: number }>();
        const polyLoops: PolyLoopBlock[] = !extractPolyLoops
            ? []
            : groupBy(
                [
                    ...[...subsequenceOverLoops, ...subsequenceOverLoopsWithTails]
                        .flatMap(x => getOrThrow(subsequencePositions, x[0]).map((p): Candidate => ({ n: x[0], p, length: x[1].length, sublength: x[1].sublength }))),
                    ...except(subsequenceLoops, [...subsequenceOverLoops.keys(), ...subsequenceOverLoopsWithTails.keys()]).flatMap(n => getOrThrow(subsequencePositions, n).map((p): Candidate => {
                        const length = p.endIndex - p.startIndex + 1;
                        return { n, p, length, sublength: length };
                    })),
                ]
                    .filter(p => !extractionParameters.loopsExcludeIfContainsSuccessiveLoopsBreak
                        || !successiveBreaksForLoops.some(b => isBetween(b.startIndex, p.p.startIndex, p.p.endIndex) && isBetween(b.endIndex, p.p.startIndex, p.p.endIndex))
                        && !loops.some(l => isBetween(p.p.startIndex, l.startIndex, l.endIndex) && isBetween(p.p.endIndex, l.startIndex, l.endIndex))),
                x => {
                    let normalization = loopNormalizations.get(x.n);
                    if (normalization === undefined) {
                        const normalizationShift = new Out<number>();
                        normalization = {
                            normalized: serializeLoop(this.getNormalizedProgression(sequence.slice(x.p.startIndex, x.p.startIndex + x.sublength), normalizationShift)),
                            normalizationShift: normalizationShift.value,
                        };
                        loopNormalizations.set(x.n, normalization);
                    }

                    return normalization.normalized;
                })
                .filter(g => !extractionParameters.loopsExcludeIfContainsAllChordsMoreThanOnce
                    || !this.containsAllChordsMoreThanOnce(sequence.slice(first(g.items).p.startIndex, first(g.items).p.startIndex + first(g.items).sublength)))
                .map(g => withPrevious(
                    orderBy(
                        groupBy(g.items
                            .filter(x => x.length === x.sublength), x => x.p.startIndex),
                        (a, b) => compareInts(a.key, b.key)))
                    .filter(x => x.previous === null || x.previous.key + 1 !== x.current.key)
                    .flatMap(x => x.current.items)
                    .map(x => ({ start: x, max: maxBy(g.items.filter(y => y.p.startIndex === x.p.startIndex), x => x.length, compareInts) }))
                    .map(x => new PolyLoopBlock({
                        normalized: g.key,
                        normalizationShift: getOrThrow(loopNormalizations, x.start.n).normalizationShift,
                        normalizationRoot: this.getLoopNormalizationRoot(roots, x.start.p.startIndex, getOrThrow(loopNormalizations, x.start.n).normalizationShift, x.start.length),
                        startIndex: x.start.p.startIndex,
                        endIndex: x.max.p.endIndex,
                        loop: sequence.slice(x.start.p.startIndex, x.start.p.startIndex + x.start.sublength),
                    })))
                .filter(g => g.length > 1 || (g.length === 1 && g[0]!.successionsSignificant))
                .flatMap(x => x);

        if (extractionParameters.sequencesExcludeIfContainsSuccessiveLoopsBreak) {
            subsequencePositions = toDictionary(
                subsequencePositions,
                x => x[0],
                x => x[1]
                    .filter(p =>
                        range(p.startIndex + 1, p.endIndex - p.startIndex)
                            .every(i => !at(successiveBreaksForSequences, i))));
        }

        // Create poly sequences
        interface PolySequenceEntry {
            readonly n: string;
            readonly c: PolySequenceBlock[];
            readonly p: Position;
            readonly s: PolySequenceBlock;
        }

        const polySequences = new Dictionary<Position, PolySequenceEntry>();
        if (extractPolySequences) {
            for (const entry of [...subsequencePositions]
                .filter(x => x[1].length > 1) // occurs more than once
                .filter(x => !extractionParameters.sequencesExcludeIfOverLoops || !subsequenceOverLoops.has(x[0]))
                .filter(x => !extractionParameters.sequencesExcludeIfOverLoopsWithTails || !subsequenceOverLoopsWithTails.has(x[0]))
                .filter(x => !extractionParameters.sequencesExcludeIfContainsAllChordsMoreThanOnce
                    || !this.containsAllChordsMoreThanOnce(sequence.slice(first(x[1]).startIndex, first(x[1]).endIndex + 1)))
                .map(x => x[0])
                .filter(
                    x => { // may not be extended without losing occurrences (unless it overlaps with itself when steps right)
                        const positions = getOrThrow(subsequencePositions, x);
                        const position = at(positions, 0);

                        if (extractionParameters.sequencesExcludeIfExtendsLeftWithoutLosingOccurrences
                            && positions.every(p => p.startIndex > 0 && (!extractionParameters.sequencesExcludeIfContainsSuccessiveLoopsBreak || !at(successiveBreaksForSequences, p.startIndex)))
                            && getOrThrow(subsequencePositions, subsequenceNormalizations.get({ startIndex: position.startIndex - 1, endIndex: position.endIndex })).length === positions.length)
                            return false;

                        if (extractionParameters.sequencesExcludeIfExtendsRightWithoutLosingOccurrencesOrOverlappingWithItself
                            && positions.every(p => p.endIndex < sequenceLength - 1 && (!extractionParameters.sequencesExcludeIfContainsSuccessiveLoopsBreak || !at(successiveBreaksForSequences, p.endIndex)))
                            && !positions.some(x => isBetween(position.endIndex + 1, x.startIndex, x.endIndex))
                            && getOrThrow(subsequencePositions, subsequenceNormalizations.get({ startIndex: position.startIndex, endIndex: position.endIndex + 1 })).length === positions.length)
                            return false;

                        return true;
                    })
                .flatMap(n => getOrThrow(subsequencePositions, n)
                    .map(p => ({ p, c: [] as PolySequenceBlock[] }))
                    .map((p): PolySequenceEntry => ({
                        n,
                        c: p.c,
                        p: p.p,
                        s: new PolySequenceBlock({
                            startIndex: p.p.startIndex,
                            endIndex: p.p.endIndex,
                            normalized: n,
                            normalizationRoot: at(roots, p.p.startIndex),
                            sequence: sequence.slice(p.p.startIndex, p.p.endIndex + 1),
                            children: [],
                        }),
                    })))) {
                polySequences.add(entry.p, entry);
            }
        }

        // Detect self overlaps
        for (const group of groupBy([...polySequences.values().map(x => x.s), ...(polyLoops as IPolyBlock[])], x => ({ normalized: x.normalized, normalizationRoot: x.normalizationRoot }))) {
            let open = 0;
            if (orderBy(
                group.items
                    .flatMap(x => [
                        ...once({ x: x.startIndex, isStart: true }),
                        { x: x.endIndex, isStart: false }]),
                (a, b) => compareInts(a.x, b.x) || compareBooleans(b.isStart, a.isStart))
                .some(p => (open += (p.isStart ? 1 : -1)) > 1)) {
                for (const polySequenceBlock of group.items) {
                    polySequenceBlock.selfOverlapsDetected = true;
                }
            }
        }

        // Fill poly sequences children (the C# fills the separate lists made with the entries, not the blocks' Children)
        for (const { c: children, p: position } of polySequences.values()) {
            children.push(...[...polySequences]
                .filter(x => isBetween(x[0].startIndex, position.startIndex, position.endIndex) && isBetween(x[0].endIndex, position.startIndex, position.endIndex))
                .map(x => x[1].s));
        }

        return { polySequences: polySequences.values().map(x => x.s), polyLoops };
    }

    private containsAllChordsMoreThanOnce(sequence: readonly CompactHarmonyMovement[]): boolean {
        let root = 0;
        const chords = new Dictionary<ChordKey, number>();
        chords.add({ root, chordType: spanAt(sequence, 0).fromType }, 1);

        for (const movement of sequence) {
            const chord: ChordKey = { root: root = Note.normalize(root + movement.rootDelta), chordType: movement.toType };
            chords.set(chord, chords.getValueOrDefault(chord, 0) + 1);
        }

        return chords.values().some(x => x === 1);
    }

    createRoots(sequence: readonly CompactHarmonyMovement[], firstRoot: number): number[] {
        return [
            ...once(firstRoot),
            ...sequence
                .map(d => firstRoot = Note.normalize(d.rootDelta + firstRoot)),
        ];
    }

    findRoundRobins(graph: BlockGraph): RoundRobinBlock[] {
        const paths = groupBy(
            [...graph.environments] // for each indexed block
                .filter(x => x[1].parents.length === 0) // without parents (top-level)
                .filter(x => !isIPolyBlock(x[0])) // not compatible with poly blocks
                .map(x => x[1].block),
            x => ({ normalized: x.normalized, normalizationRoot: x.normalizationRoot, type: x.type })) // find all groups of the same blocks
            .flatMap(g => asPairs(orderBy(g.items, (a, b) => compareInts(a.startIndex, b.startIndex))) // and each group will return all paths for every consecutive pair in it
                .filter(p => ((path: IBlockJoint[]) =>
                    path.length > 1
                    && !anyDuplicates(path
                        .map(j => j.block2.block)
                        .map(b => ({ normalized: b.normalized, normalizationRoot: b.normalizationRoot }))))(this.pathsSearcher.dijkstra(graph, p.previous, p.current)))
                .map(p => orderBy(
                    distinct(range(p.previous.startIndex, p.current.endIndex - p.previous.startIndex + 1) // at every position between the blocks
                        .flatMap(i => graph.blocksAt.get(i)) // get all distinct blocks
                        .filter(x => !isIPolyBlock(x))) // not compatible with poly blocks
                        .filter(x => x.startIndex >= p.previous.startIndex && x.endIndex <= p.current.endIndex) // only blocks fully within the current segment
                        .filter(x => getOrThrow(graph.environments, x).parents.length === 0), // top-level
                    (a, b) => compareInts(a === p.previous ? 0 : 1, b === p.previous ? 0 : 1) // let the from-block go first
                        || compareInts(a === p.current ? 1 : 0, b === p.current ? 1 : 0) // and the to-block go last
                        || compareInts(a.startIndex, b.startIndex) // rest everything is in order
                        || compareInts(a.endIndex, b.endIndex)
                        || compareStrings(a.normalized, b.normalized))) // any order works ok for the purpose of invariant normalizations
                .filter(p => {
                    if (p.length > 2) return true;
                    throw new Error('Could never have happened.');
                }));

        // now we've got all different paths, many of them are overlapping (they should turn to the same round-robin block)
        return groupBy(paths, p => { // find groups of the same normalization
            const normalizationRoot = new Out<number>();
            return { item1: RoundRobinBlock.getNormalization(p, p.length - 1, normalizationRoot), normalizationRoot: normalizationRoot.value };
        })
            .flatMap(g => toChunksByShouldStartNew( // for each group, split to chunks of non-overlapping paths
                withPrevious(orderBy(g.items, (a, b) => compareInts(at(a, 0).startIndex, at(b, 0).startIndex))),
                p => p.previous !== null && atFromEnd(p.previous, 1).startIndex < at(p.current, 0).startIndex) // if there's a gap between the last of the previous and the first of the current, it's a new block
                .map(c => new RoundRobinBlock(
                    distinct(c.flatMap(x => x.current)),
                    first(c).current.length - 1))
                // quick testing showed that this condition is not needed, the segments previously covered by existing blocks are now not found (probably due to the top-level requirement)
                .filter(b => graph.blocksByPositions.get({ startIndex: b.startIndex, endIndex: b.endIndex }).every(x => !(x.type === BlockType.RoundRobin || x.type === BlockType.RoundRobinLx))));
    }

    findPingPongs(graph: BlockGraph): PingPongBlock[] {
        return groupBy(
            graph.joints
                .flatMap(l => l.block2.rightJoints.map(r => ({ l, r })))
                .filter(x => x.l.block1.block.type === x.r.block2.block.type
                    && x.l.block1.block.normalized === x.r.block2.block.normalized
                    && x.l.block1.block.normalizationRoot === x.r.block2.block.normalizationRoot),
            x => {
                const normalizations = orderBy([...once(x.l.normalization), x.r.normalization], compareStrings);
                return normalizations[0]! + normalizations[1]!;
            })
            .flatMap(pingPongsOfTheNormalization => toChunksByShouldStartNew(withPrevious(pingPongsOfTheNormalization.items), p => p.current.l !== (p.previous?.r ?? null))
                // find consecutive joints, each subgroup will become a ping pong
                .map(c => c.map(p => p.current)))
            .map(x => {
                const children = orderBy(distinct(x.flatMap(x => [...once(x.l.block1.block), x.r.block1.block, x.r.block2.block])), (a, b) => compareInts(a.startIndex, b.startIndex));
                if (anyDuplicates(children.map(x => x.startIndex))
                    || anyDuplicates(children.map(x => x.endIndex)))
                    throw new Error('Unique blocks start indices expected.');
                return new PingPongBlock({
                    children,
                });
            });
    }

    findLoops(sequence: readonly CompactHarmonyMovement[], roots: readonly number[]): LoopBlock[] {
        const indices = new Dictionary<ChordKey, number>();
        indices.add({ root: at(roots, 0), chordType: spanAt(sequence, 0).fromType }, -1);

        const loops: LoopBlock[] = [];

        let movementIndex = 0;
        while (movementIndex < sequence.length) {
            const key: ChordKey = { root: at(roots, movementIndex + 1), chordType: spanAt(sequence, movementIndex).toType };
            if (indices.tryAdd(key, movementIndex)) {
                movementIndex++;
                continue;
            }

            const movementIndexToLoopStart = indices.get(key);
            const loopStartRoot = at(roots, movementIndexToLoopStart + 1);
            if (loopStartRoot !== at(roots, movementIndex + 1))
                throw new Error('Could not have happened.');

            if ((movementIndexToLoopStart === -1 ? spanAt(sequence, 0).fromType : spanAt(sequence, movementIndexToLoopStart).toType) !== spanAt(sequence, movementIndex).toType)
                throw new Error('Could not have happened.');

            const foundLoop = sequence.slice(movementIndexToLoopStart + 1, movementIndexToLoopStart + 1 + movementIndex - movementIndexToLoopStart);
            let movementIndexInLoop = 0;
            movementIndex++;
            while (movementIndex <= sequence.length) {
                if (movementIndex < sequence.length
                    && spanAt(sequence, movementIndex).equals(spanAt(foundLoop, movementIndexInLoop))) {
                    movementIndexInLoop = intMod(movementIndexInLoop + 1, foundLoop.length);
                    movementIndex++;
                    continue;
                }

                break;
            }

            movementIndex--; // now points to the movement leading to the last root of the found sequence

            const normalizationShift = new Out<number>();
            const normalized = serializeLoop(this.getNormalizedProgression(foundLoop, normalizationShift, new Out<number>()));
            loops.push(new LoopBlock({
                loop: foundLoop,
                normalized,
                normalizationShift: normalizationShift.value,
                normalizationRoot: this.getLoopNormalizationRoot(roots, movementIndexToLoopStart + 1, normalizationShift.value, foundLoop.length),
                startIndex: movementIndexToLoopStart + 1,
                endIndex: movementIndex,
            }));

            movementIndex -= foundLoop.length - 1;
            indices.clear();
        }

        return loops;
    }

    private getLoopNormalizationRoot(roots: readonly number[], startIndex: number, normalizationShift: number, loopLength: number): number {
        return at(roots, startIndex + this.invertNormalizationShift(normalizationShift, loopLength));
    }

    findSelfJumps(sequence: readonly CompactHarmonyMovement[], loops: readonly LoopBlock[]): LoopSelfMultiJumpBlock[] {
        return groupBy(withIndices(loops), x => x.x.normalized)
            .flatMap(g => toChunks(withPrevious(g.items), x => {
                if (x.previous === null) return false;
                const difference = x.current.i - x.previous.i;
                if (difference > 2) return false;
                if (difference < 1) throw new Error('Could not have happened, indices are ascending.');
                return x.current.x.startIndex - x.previous.x.endIndex < 3;
            })
                .filter(c => c.criterium)
                .map(c => ({
                    normalized: g.key,
                    indices: [...once(first(c.chunk).previous!.i), ...c.chunk.map(x => x.current.i)],
                })))
            .map(j => {
                const firstLoop = at(loops, at(j.indices, 0));
                return new LoopSelfMultiJumpBlock({
                    childLoops: range(at(j.indices, 0), maxInt(j.indices) - at(j.indices, 0) + 1).map(x => at(loops, x)),
                    normalizationRootsFlow: toChunks(j.indices
                        .map(x => at(loops, x))
                        .filter(x => x.normalized === firstLoop.normalized)
                        .map(x => x.normalizationRoot), x => x)
                        .map(x => x.criterium),
                    childJumps: asPairs(j.indices
                        .filter(x => at(loops, x).normalized === firstLoop.normalized))
                        .map(p => {
                            const previousLoop = at(loops, p.previous);
                            const currentLoop = at(loops, p.current);

                            const jointLoop = p.previous === p.current - 1 ? null : at(loops, p.current - 1);
                            const loopsGap = currentLoop.startIndex - previousLoop.endIndex;

                            // 1 means sequential, 2 means one movement in between
                            if (loopsGap > 2)
                                throw new ArgumentOutOfRangeException(undefined, `The gap cannot be more than 1. (Parameter 'loopsGap')\nActual value was ${loopsGap}.`);

                            let jointMovement: CompactHarmonyMovement | null;
                            if (loopsGap === 2) jointMovement = spanAt(sequence, previousLoop.endIndex + 1);
                            else if (loopsGap === 1) jointMovement = null; // single ambiguous chord
                            else if (loopsGap < 1) jointMovement = null; // multiple ambiguous chords, overlap
                            else throw new ArgumentOutOfRangeException();

                            const result = new LoopSelfJumpBlock({
                                loop1: previousLoop,
                                loop2: currentLoop,
                                jointLoop,
                                jointMovement,
                            });

                            return result;
                        }),
                });
            });
    }

    findSequenceBlocks(sequence: readonly CompactHarmonyMovement[], existingBlocks: readonly IBlock[], roots: readonly number[]): SequenceBlock[] {
        return toChunksByShouldStartNew(
            withPrevious(orderBy(
                except(range(0, sequence.length), existingBlocks
                    .filter(isIIndexedBlock)
                    .filter(x => !isIPolyBlock(x)) // the sequences search (to make the graph connected) will not notice the poly blocks because some algorithms (like the round-robin search) ignore poly blocks but require sequences.
                    .flatMap(b => range(b.startIndex, b.blockLength))),
                compareInts)),
            x => x.previous === null || x.previous + 1 !== x.current)
            .map(c => {
                const startIndex = at(c, 0).current;
                const endIndex = atFromEnd(c, 1).current;

                const subsequence = sequence.slice(startIndex, endIndex + 1);

                return new SequenceBlock({
                    startIndex,
                    endIndex,
                    sequence: subsequence,
                    normalized: serializeLoop(subsequence),
                    normalizationRoot: at(roots, startIndex),
                });
            });
    }

    findMassiveOverlapsBlocks(loopBlocks: readonly LoopBlock[]): LoopMassiveOverlapsBlock[] {
        const result: LoopMassiveOverlapsBlock[] = [];
        const currentOpenLoops: LoopBlock[] = [];
        let currentMassiveOverlapsBlock: LoopBlock[] | null = null;
        for (const { isStart, loop } of orderBy(
            loopBlocks
                .flatMap(x => [
                    { i: x.startIndex, isStart: true, loop: x },
                    { i: x.endIndex, isStart: false, loop: x },
                ]),
            (a, b) => compareInts(a.i, b.i) || compareBooleans(b.isStart, a.isStart))) {
            if (isStart) {
                currentOpenLoops.push(loop);
            }

            if (currentOpenLoops.length > 2) {
                if (currentMassiveOverlapsBlock === null) {
                    currentMassiveOverlapsBlock = [...currentOpenLoops];
                } else if (!currentMassiveOverlapsBlock.includes(loop))
                    currentMassiveOverlapsBlock.push(loop);
            }

            if (!isStart) {
                remove(currentOpenLoops, loop);

                if (currentOpenLoops.length === 1 && currentMassiveOverlapsBlock !== null) {
                    const firstLoop = at(currentMassiveOverlapsBlock, 0);
                    const lastLoop = atFromEnd(currentMassiveOverlapsBlock, 1);
                    removeAt(currentMassiveOverlapsBlock, 0);
                    removeAt(currentMassiveOverlapsBlock, currentMassiveOverlapsBlock.length - 1);
                    result.push(new LoopMassiveOverlapsBlock({
                        startIndex: firstLoop.startIndex,
                        endIndex: lastLoop.endIndex,
                        edge1: firstLoop,
                        edge2: lastLoop,
                        internalLoops: currentMassiveOverlapsBlock,
                    }));

                    currentMassiveOverlapsBlock = null;
                }
            }
        }

        if (currentOpenLoops.length !== 0 || currentMassiveOverlapsBlock !== null) {
            throw new Error('Could not have happened.');
        }

        return result;
    }

    findBlocks(sequence: readonly CompactHarmonyMovement[], roots: readonly number[], blockTypes: readonly BlockType[], polyBlocksExtractionParameters: PolyBlocksExtractionParameters | null = null): IBlock[] {
        const blocks: IBlock[] = [];

        if (blockTypes.includes(BlockType.Loop)) {
            const loops = this.findLoops(sequence, roots);
            blocks.push(...loops);

            if (blockTypes.includes(BlockType.LoopSelfJump) || blockTypes.includes(BlockType.LoopSelfMultiJump)) {
                const jumps = this.findSelfJumps(sequence, loops);
                if (blockTypes.includes(BlockType.LoopSelfMultiJump)) {
                    blocks.push(...jumps);
                }

                if (blockTypes.includes(BlockType.LoopSelfJump)) {
                    blocks.push(...jumps.flatMap(x => x.childJumps));
                }
            }

            if (blockTypes.includes(BlockType.MassiveOverlaps)) {
                blocks.push(...this.findMassiveOverlapsBlocks(loops));
            }

            const extractPolySequences = blockTypes.includes(BlockType.PolySequence);
            const extractPolyLoops = blockTypes.includes(BlockType.PolyLoop);
            if (extractPolySequences || extractPolyLoops) {
                const { polySequences, polyLoops } = this.findPolyBlocks(sequence, roots, loops, extractPolySequences, extractPolyLoops, polyBlocksExtractionParameters ?? new PolyBlocksExtractionParameters());
                if (extractPolySequences) {
                    blocks.push(...polySequences);
                }

                if (extractPolyLoops) {
                    blocks.push(...polyLoops);
                }
            }
        }

        if (blockTypes.includes(BlockType.Sequence)) {
            blocks.push(...this.findSequenceBlocks(sequence, blocks, roots));
        }

        if (blockTypes.includes(BlockType.PingPong) || blockTypes.includes(BlockType.RoundRobin)) {
            if (blockTypes.includes(BlockType.RoundRobin)) {
                while (true) {
                    const graph = this.createGraph(blocks);
                    const roundRobins = this.findRoundRobins(graph);
                    if (roundRobins.length === 0) {
                        break;
                    }

                    blocks.push(...roundRobins);
                    if (!blockTypes.includes(BlockType.RoundRobinLx)) {
                        break;
                    }
                }
            }

            if (blockTypes.includes(BlockType.PingPong)) {
                const graph = this.createGraph(blocks);
                blocks.push(...this.findPingPongs(graph));
            }
        }

        if (blockTypes.includes(BlockType.SequenceStart)) {
            blocks.push(new EdgeBlock(BlockType.SequenceStart, sequence.length));
        }

        if (blockTypes.includes(BlockType.SequenceEnd)) {
            blocks.push(new EdgeBlock(BlockType.SequenceEnd, sequence.length));
        }

        listSort(blocks, (block1, block2) => {
            const x = compareInts(block1.startIndex, block2.startIndex);
            if (x !== 0) return x;
            if (block1 === block2) return 0;
            return -compareInts(block1.endIndex, block2.endIndex);
        });

        return blocks;
    }

    private static getChildBlocksSubtree(block: IIndexedBlock): IIndexedBlock[] {
        return block.children.flatMap(b => [b, ...IndexExtractor.getChildBlocksSubtree(b)]);
    }

    createGraph(blocks: readonly IBlock[]): BlockGraph {
        const environments = toDictionary(
            blocks.filter(isIIndexedBlock).map(b => new BlockEnvironment({
                block: b,
            })),
            x => x.block,
            x => x);

        for (const environment of environments.values()) {
            environment.children.push(...environment.block.children.map(c => getOrThrow(environments, c)));
            environment.childrenSubtree.push(...distinct(IndexExtractor.getChildBlocksSubtree(environment.block).map(c => getOrThrow(environments, c))));
        }

        for (const { p, c } of [...environments.values()].flatMap(p => p.childrenSubtree.map(c => ({ p, c })))) {
            c.parents.push(p);
        }

        const environmentValues = [...environments.values()];
        const joints = environmentValues.flatMap((b1, i1) => environmentValues
            .filter((b2, i2) =>
                i2 > i1
                && !(b1.block.startIndex > b2.block.endIndex + 1 || b2.block.startIndex > b1.block.endIndex + 1)
                && !b1.childrenSubtree.includes(b2)
                && !b2.childrenSubtree.includes(b1)

                // neither of them is fully within another
                && [b1.block.startIndex, b1.block.endIndex].some(x => !isBetween(x, b2.block.startIndex, b2.block.endIndex))
                && [b2.block.startIndex, b2.block.endIndex].some(x => !isBetween(x, b1.block.startIndex, b1.block.endIndex)))
            .map(b2 => {
                switch (compareInts(b1.block.startIndex, b2.block.startIndex)) {
                    case -1:
                        return { b1, b2 };
                    case 1:
                        return { b1: b2, b2: b1 };
                    case 0:
                        throw new Error('The blocks starts may not be the same.');
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }))
            .map(x => new BlockJoint({
                block1: x.b1,
                block2: x.b2,
            }));

        for (const joint of joints) {
            joint.block1.rightJoints.push(joint);
            joint.block2.leftJoints.push(joint);
        }

        const uselessLoops = environmentValues.filter(e => {
            if (!(e.block instanceof LoopBlock))
                return false;

            const left = e.leftJoints.map(x => x.block1.block).filter((x): x is LoopBlock => x instanceof LoopBlock);
            const right = e.rightJoints.map(x => x.block2.block).filter((x): x is LoopBlock => x instanceof LoopBlock);
            return left.length === 1
                && right.length === 1
                && left[0]!.endIndex + 1 >= right[0]!.startIndex;
        });

        for (const environment of uselessLoops) {
            environment.detections |= BlockDetections.UselessLoop;
        }

        return new BlockGraph({
            environments: new Map<IIndexedBlock, IBlockEnvironment>(environments),
            joints,
            blocksAt: new Lookup([...environments.keys()]
                .flatMap(b => range(b.startIndex, b.endIndex - b.startIndex + 1).map(i => ({ b, i })))
                .map(x => [x.i, x.b] as const)),
            blocksByPositions: new Lookup([...environments.keys()]
                .map(x => [{ startIndex: x.startIndex, endIndex: x.endIndex }, x] as const)),
        });
    }

    /**
     * Between 0 (inclusive) and progression length (exclusive).
     * Inverts the number of steps one progression is ahead to the number of steps it is behind,
     * where a progression may be the normalized progression or the original progression.
     * Unaware of invariants.
     */
    invertNormalizationShift(normalizationShift: number, progressionLength: number): number {
        return intMod(progressionLength - normalizationShift, progressionLength);
    }

    /**
     * normalizationShift:
     * Between 0 (inclusive) and progression length (exclusive).
     * Returns the index of the start of the original progression in the normalized progression.
     * In other words, the number of steps the original progression is ahead or the normalized progression is behind.
     * For invariants > 1, returns the minimal possible shift, i.e. between 0 (inclusive) and (progression length / invariants) (exclusive).
     */
    getNormalizedProgression(
        progression: readonly CompactHarmonyMovement[],
        normalizationShift: Out<number> = new Out<number>(),
        invariants: Out<number> = new Out<number>()): readonly CompactHarmonyMovement[] {
        // BitConverter.ToInt32 of the little-endian bytes [RootDelta, FromType, ToType, 0]
        const idSequences = progression.map(p => p.rootDelta | (p.fromType << 8) | (p.toType << 16));

        const length = progression.length;
        let shifts = range(0, length);
        let iteration = 0;
        invariants.value = 1;
        while (shifts.length > 1) {
            if (iteration === length) { // multiple shifts possible
                invariants.value = shifts.length;
                break;
            }

            const currentIteration = iteration;
            shifts = minBy(
                groupBy(shifts, s => at(idSequences, intMod(s + currentIteration, length))),
                g => g.key,
                compareInts)
                .items;

            iteration++;
        }

        normalizationShift.value = minInt(shifts
            .map(x => this.invertNormalizationShift(x, length)));
        return range(first(shifts), length)
            .map(s => at(progression, intMod(s, length)));
    }

    findStructureLinks(externalId: string, compactChordsProgression: CompactChordsProgression): StructureLink[] {
        const loopResults = new Dictionary<
            { readonly normalized: string; readonly normalizationRoot: number },
            { readonly occurrences: number; readonly successions: number; readonly length: number }>();
        for (const extendedHarmonyMovementsSequence of compactChordsProgression.extendedHarmonyMovementsSequences) {
            const roots = this.createRoots(extendedHarmonyMovementsSequence.movements, extendedHarmonyMovementsSequence.firstRoot);
            const loops = this.findLoops(extendedHarmonyMovementsSequence.movements, roots);

            for (const loop of loops) {
                const key = { normalized: loop.normalized, normalizationRoot: loop.normalizationRoot };
                const counters = loopResults.getValueOrDefault(key, { occurrences: 0, successions: 0, length: 0 });
                loopResults.set(key, {
                    occurrences: toFloat(counters.occurrences + loop.occurrences),
                    successions: toFloat(counters.successions + loop.successions),
                    length: toShort(counters.length + loop.blockLength),
                });
            }
        }

        const items = [...loopResults]
            .map(p => new StructureLink(
                p[0].normalized,
                externalId,
                p[0].normalizationRoot,
                p[1].occurrences,
                p[1].successions,
                toFloat(p[1].length / sumInt(compactChordsProgression.extendedHarmonyMovementsSequences.map(s => s.movements.length)))));
        return items;
    }
}
