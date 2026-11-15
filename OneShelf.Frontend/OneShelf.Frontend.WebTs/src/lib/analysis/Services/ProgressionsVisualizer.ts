import { ArgumentException, InvalidCastException, KeyNotFoundException } from '../../common/dotnet/exceptions';
import { at, atFromEnd, compareStrings, first, groupBy, maxNullableInt, orderBy, range, spanAt } from '../../common/dotnet/Linq';
import { formatN, formatP, intDiv } from '../../common/dotnet/Numbers';
import { Out } from '../../common/dotnet/Out';
import { Note, RepresentationSettings } from '../../representations';
import { once, withIndices } from '../../common/OneShelf.Common/Extensions';
import type { ChordsProgression } from '../Models/ChordsProgression';
import type { CompactHarmonyMovement } from '../Models/CompactV1/CompactHarmonyMovement';
import { EdgeBlock } from '../Models/Index/Blocks/EdgeBlock';
import type { IBlock } from '../Models/Index/Blocks/Interfaces/IBlock';
import type { IIndexedBlock } from '../Models/Index/Blocks/Interfaces/IIndexedBlock';
import { isIIndexedBlock } from '../Models/Index/Blocks/Interfaces/IIndexedBlock';
import { isIPolyBlock } from '../Models/Index/Blocks/Interfaces/IPolyBlock';
import { LoopBlock } from '../Models/Index/Blocks/LoopBlock';
import { LoopBlockBase } from '../Models/Index/Blocks/LoopBlockBase';
import { LoopMassiveOverlapsBlock } from '../Models/Index/Blocks/LoopMassiveOverlapsBlock';
import { LoopSelfJumpBlock } from '../Models/Index/Blocks/LoopSelfJumpBlock';
import { LoopSelfMultiJumpBlock } from '../Models/Index/Blocks/LoopSelfMultiJumpBlock';
import { PingPongBlock } from '../Models/Index/Blocks/PingPongBlock';
import { PolyLoopBlock } from '../Models/Index/Blocks/PolyLoopBlock';
import { PolySequenceBlock } from '../Models/Index/Blocks/PolySequenceBlock';
import { RoundRobinBlock } from '../Models/Index/Blocks/RoundRobinBlock';
import { SequenceBlock } from '../Models/Index/Blocks/SequenceBlock';
import { BlockDetections } from '../Models/Index/Enums/BlockDetections';
import { blockTypeToString } from '../Models/Index/Enums/BlockType';
import type { BlockGraph } from '../Models/Index/Graphs/BlockGraph';
import type { IBlockJoint } from '../Models/Index/Graphs/Interfaces/IBlockJoint';
import type { StructureLink } from '../Models/Structure/StructureLink';
import type { BlocksChartParameters } from '../Models/TextGraphics/BlocksChartParameters';
import { Text } from '../Models/TextGraphics/Text';
import { asText } from '../Models/TextGraphics/TextExtensions';
import { chordTypeToString } from '../Tools/Extensions';
import { getTitleFromBeginningNote } from '../Tools/TonalitiesExtensions';
import type { SearchResult } from './ProgressionsSearch';

const Bullet = '\u25e6';
const Times = '\u00d7';

/** (Text left, Text right) */
export interface Line {
    readonly left: Text;
    readonly right: Text;
}

/** GetType().Name of the blocks. */
function typeName(block: IBlock): string {
    if (block instanceof LoopBlock) return 'LoopBlock';
    if (block instanceof PolyLoopBlock) return 'PolyLoopBlock';
    if (block instanceof SequenceBlock) return 'SequenceBlock';
    if (block instanceof PolySequenceBlock) return 'PolySequenceBlock';
    if (block instanceof PingPongBlock) return 'PingPongBlock';
    if (block instanceof RoundRobinBlock) return 'RoundRobinBlock';
    if (block instanceof EdgeBlock) return 'EdgeBlock';
    if (block instanceof LoopSelfJumpBlock) return 'LoopSelfJumpBlock';
    if (block instanceof LoopSelfMultiJumpBlock) return 'LoopSelfMultiJumpBlock';
    if (block instanceof LoopMassiveOverlapsBlock) return 'LoopMassiveOverlapsBlock';
    throw new Error('Unknown block type.');
}

/** Cast<T>().First(): the first element, which must be a T. */
function castFirst<T>(items: readonly IBlock[], type: abstract new (...args: never[]) => T): T {
    const item = first(items);
    if (!(item instanceof type)) throw new InvalidCastException();
    return item;
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

export class ProgressionsVisualizer {
    static readonly AttributeSearch = 'search';
    static readonly AttributeSearchFirst = 'search-first';

    getLoopStatisticsTitle(link: StructureLink): string {
        return `${formatN(link.successions, 2)}/${formatN(link.occurrences, 2)}, ${formatP(link.coverage, 0)}%`;
    }

    getLoopChordsTitle(link: StructureLink): string {
        return getTitleFromBeginningNote(link.normalized, link.normalizationRoot, true);
    }

    buildCustomAttributesForSearch(progression: ChordsProgression, searchResult: SearchResult): ReadonlyMap<number, string> {
        return toDictionary(
            withIndices(progression.originalSequence)
                .map(x => {
                    const isFirst = searchResult.harmonyGroupsWithIsFirst.get(x.x.harmonyGroup);
                    return {
                        x: x.x,
                        i: x.i,
                        isFirst: isFirst !== undefined
                            ? x.x.indexInHarmonyGroup === 0 && isFirst
                            : null,
                    };
                })
                .filter(x => x.isFirst !== null),
            x => x.i,
            x => x.isFirst! ? ProgressionsVisualizer.AttributeSearchFirst : ProgressionsVisualizer.AttributeSearch);
    }

    visualizeBlocksAsOne(sequence: readonly CompactHarmonyMovement[], roots: readonly number[], blocks: readonly IBlock[], graph: BlockGraph, shortestPath: readonly IBlockJoint[], parameters: BlocksChartParameters): Text {
        const lines = this.visualizeBlocks(sequence, roots, blocks, graph, shortestPath, parameters);
        return Text.join(Text.NewLine, lines.map(x => asText(`${x.left}   ${x.right}`)));
    }

    visualizeBlocksAsTwo(sequence: readonly CompactHarmonyMovement[], roots: readonly number[], blocks: readonly IBlock[], graph: BlockGraph, shortestPath: readonly IBlockJoint[], parameters: BlocksChartParameters): Line {
        const blockVisualizations = this.visualizeBlocks(sequence, roots, blocks, graph, shortestPath, parameters);

        const left = Text.join(Text.NewLine, blockVisualizations.map(v => v.left));
        const right = Text.join(Text.NewLine, blockVisualizations.map(v => v.right));

        return { left, right };
    }

    private visualizeBlocks(sequence: readonly CompactHarmonyMovement[], roots: readonly number[], blocks: readonly IBlock[], graph: BlockGraph, shortestPath: readonly IBlockJoint[], parameters: BlocksChartParameters): Line[] {
        const positions = new Out<number[]>();
        const rootsTrace = this.createRootsTraceByIndices(sequence, roots, positions, parameters.typesToo);

        const gridPositions = positions.value.filter((_, i) => i % 6 === 5);

        const shortestPathBlocks = new Set([...once(at(shortestPath, 0).block1), ...shortestPath.map(x => x.block2)].map(x => x.block));

        const lines: Line[] = [
            { left: asText(rootsTrace), right: Text.EmptyLineContent },
        ];

        const blockGroups = new Out<Map<IIndexedBlock, number>>();
        lines.push(...ProgressionsVisualizer.createBlocksLines(blocks, graph, parameters, positions.value, rootsTrace.length, shortestPathBlocks, gridPositions, blockGroups));

        if (parameters.showJoints) {
            const jointLines = ProgressionsVisualizer.createJointsLines(graph, positions.value, rootsTrace.length);
            if (jointLines.length > 0) {
                lines.push({ left: Text.EmptyLineContent, right: Text.EmptyLineContent });
                lines.push(...jointLines);
            }
        }

        if (parameters.showPath) {
            lines.push({ left: Text.EmptyLineContent, right: Text.EmptyLineContent });
            const pathLines = this.createPathLines(shortestPath, blockGroups.value);
            lines.push(...pathLines);
        }

        return lines;
    }

    private createPathLines(shortestPath: readonly IBlockJoint[], blockGroups: Map<IIndexedBlock, number>): Line[] {
        const titles = toDictionary(
            [
                ...[...blockGroups].map(x => ({ key: x[0], title: x[1].toString() })),
                { key: at(shortestPath, 0).block1.block, title: 'ss' },
                { key: atFromEnd(shortestPath, 1).block2.block, title: 'se' },
            ],
            x => x.key,
            x => x.title);

        const pathVisualization = groupBy(shortestPath, x => ({ item1: x.block1.block.normalized, item2: x.block2.block.normalized }))
            .map(g => {
                const path = asText(`${getOrThrow(titles, first(g.items).block1.block)} -> ${getOrThrow(titles, first(g.items).block2.block)}`);
                const counts = groupBy(g.items, x => x.normalization).map(x => x.items.length);
                path.appendText(counts.length === 1 && counts[0] === 1
                    ? Text.Empty
                    : counts.length === 1
                        ? asText(` ${Times}${counts[0]}`)
                        : asText(counts.map(x => ` ${Times}${x}`).join(','), Text.CssTextRed));

                return path;
            });

        return pathVisualization.map((x): Line => ({ left: x, right: Text.EmptyLineContent }));
    }

    private static createJointsLines(graph: BlockGraph, positions: readonly number[], rootsTraceLength: number): Line[] {
        const jointTitles = toDictionary(
            groupBy(
                groupBy(graph.joints.filter(x => !x.isEdge), x => x.normalization)
                    .flatMap((g, i) =>
                        g.items.map(j => ({ title: String.fromCharCode(('a'.charCodeAt(0) + i) & 0xffff), positionIndex: at(positions, j.block2.block.startIndex) }))),
                x => x.positionIndex),
            x => x.key,
            x => orderBy(x.items.map(x => x.title), compareStrings));

        return range(0, maxNullableInt([...jointTitles.values()].map(x => x.length)) ?? 0)
            .reverse()
            .map((lineIndex): Line => ({
                left: asText(
                    range(0, rootsTraceLength)
                        .map(i => ((t: string[] | undefined) => t === undefined ? ' ' : lineIndex < t.length ? t[lineIndex]! : ' ')(jointTitles.get(i)))
                        .join('')),
                right: lineIndex === 0 ? asText(`joints: ${graph.joints.length}`) : Text.Empty,
            }));
    }

    private static createBlocksLines(
        blocks: readonly IBlock[],
        graph: BlockGraph,
        parameters: BlocksChartParameters,
        positions: readonly number[],
        rootsTraceLength: number,
        shortestPathBlocks: Set<IIndexedBlock>,
        gridPositions: readonly number[],
        blockGroups: Out<Map<IIndexedBlock, number>>): Line[] {
        const groups = groupBy(
            blocks.filter(x => !(x instanceof EdgeBlock)),
            x => {
                if (x instanceof LoopBlockBase && parameters.groupNormalized) return blockTypeToString(x.type) + x.normalized;
                if (x instanceof SequenceBlock && parameters.groupNormalized) return 'S' + x.normalized;
                if (x instanceof PingPongBlock && parameters.groupNormalized) return 'PP' + x.normalized;
                if (x instanceof RoundRobinBlock && parameters.groupNormalized) return 'RR' + x.normalized;
                if (x instanceof PolySequenceBlock && parameters.groupNormalized) return 'PS' + x.normalized;
                return Math.random().toString();
            });

        blockGroups.value = toDictionary(
            withIndices(groups).flatMap(g => g.x.items.filter(isIIndexedBlock).map(b => ({ b, i: g.i }))),
            x => x.b,
            x => x.i + 1);

        return withIndices(groups)
            .flatMap(pair => {
                const { x: grouping, i } = pair;

                const periodPositions: number[] = [];
                const almostPeriodPositions: number[] = [];
                const polySequencePeriodPositions: number[] = [];
                for (const polySequenceBlock of grouping.items.filter((x): x is PolySequenceBlock => x instanceof PolySequenceBlock)) {
                    polySequencePeriodPositions.push(at(positions, polySequenceBlock.startIndex));
                    polySequencePeriodPositions.push(at(positions, polySequenceBlock.endIndex + 1));
                }

                for (const loop of grouping.items.filter((x): x is LoopBlockBase => x instanceof LoopBlockBase)) {
                    periodPositions.push(...range(0, intDiv(loop.blockLength, loop.loopLength) + 1)
                        .map(x => x * loop.loopLength + loop.startIndex)
                        .map(x => at(positions, x)));

                    if (loop.eachChordCoveredTimesWhole) {
                        almostPeriodPositions.push(at(positions, loop.endIndex + 1));
                    }
                }

                const left = Text.join(
                    Text.Empty,
                    range(0, rootsTraceLength)
                        .map(j => {
                            const found = grouping.items.filter(b => at(positions, b.startIndex) <= j && at(positions, b.endIndex + 1) >= j);

                            const uselessLoop = found.every(x => isIIndexedBlock(x) && (getOrThrow(graph.environments, x).detections & BlockDetections.UselessLoop) === BlockDetections.UselessLoop);
                            const shortestPathLoop = found.some(x => shortestPathBlocks.has(x as IIndexedBlock));

                            const firstFound = found[0];
                            const isModulation =
                                firstFound instanceof LoopBlockBase ? firstFound.normalizationRoot !== castFirst(grouping.items, LoopBlockBase).normalizationRoot
                                    : firstFound instanceof RoundRobinBlock ? firstFound.normalizationRoot !== castFirst(grouping.items, RoundRobinBlock).normalizationRoot
                                        : firstFound instanceof SequenceBlock ? firstFound.normalizationRoot !== castFirst(grouping.items, SequenceBlock).normalizationRoot
                                            : firstFound instanceof PolySequenceBlock ? firstFound.normalizationRoot !== castFirst(grouping.items, PolySequenceBlock).normalizationRoot
                                                : false;

                            const selfOverlapsDetected = grouping.items.filter(isIPolyBlock).some(x => x.selfOverlapsDetected);

                            const css = uselessLoop
                                ? Text.CssTextLightYellow
                                : selfOverlapsDetected
                                    ? Text.CssTextRed
                                    : null;

                            if (found.length === 0)
                                return gridPositions.includes(j)
                                    ? asText('+', Text.CssTextLightGray)
                                    : asText(' ');

                            let char: string | null = null;
                            if (found.length > 1) {
                                const starts = found.some(x => at(positions, x.startIndex) === j);
                                const ends = found.some(x => at(positions, x.endIndex + 1) === j);
                                const middles = found.some(x => at(positions, x.startIndex) !== j && at(positions, x.endIndex + 1) !== j);
                                char = starts && ends
                                    ? 'X'
                                    : starts && middles
                                        ? '<'
                                        : ends && middles
                                            ? '>'
                                            : null;
                            }

                            return asText(char
                                ?? (periodPositions.includes(j)
                                    ? '|'
                                    : polySequencePeriodPositions.includes(j)
                                        ? '+'
                                        : almostPeriodPositions.includes(j)
                                            ? Bullet
                                            : isModulation
                                                ? '~'
                                                : shortestPathLoop
                                                    ? '='
                                                    : '-'), css);
                        }));

                const selfOverlap = grouping.items.filter(isIPolyBlock).some(x => x.selfOverlapsDetected) ? 'SOLP ' : null;
                const roundRobinLevel = ((x: number) => x > 0 ? `L${x} ` : null)(grouping.items.filter((x): x is RoundRobinBlock => x instanceof RoundRobinBlock).map(x => x.level)[0] ?? 0);
                const firstOfGrouping = first(grouping.items);
                const title = grouping.items.length === 1
                    ? typeName(firstOfGrouping)
                    : `${firstOfGrouping instanceof PingPongBlock || firstOfGrouping instanceof PolySequenceBlock || firstOfGrouping instanceof PolyLoopBlock || firstOfGrouping instanceof RoundRobinBlock ? typeName(firstOfGrouping) : grouping.key} ${Times}${grouping.items.length}`;

                const right = asText(`${i + 1}: ${selfOverlap ?? ''}${roundRobinLevel ?? ''}${title}`);

                return once<Line>({ left, right });
            });
    }

    createRootsTraceByIndices(
        sequence: readonly CompactHarmonyMovement[],
        roots: readonly number[],
        positions: Out<number[]>,
        typesToo = true,
        block: IBlock | null = null): string {
        const startIndex = block?.startIndex ?? 0;
        const endIndex = block?.endIndex ?? (sequence.length - 1);

        let builder = '';
        if (typesToo) {
            builder += new Note(at(roots, startIndex)).representation(new RepresentationSettings());
            builder += chordTypeToString(spanAt(sequence, startIndex).fromType);
        } else {
            builder += at(roots, startIndex).toString();
        }

        positions.value =
        [
            0,
        ];

        for (let i = startIndex; i <= endIndex; i++) {
            builder += ' ';

            const startPosition = builder.length;
            if (typesToo) {
                builder += new Note(at(roots, i + 1)).representation(new RepresentationSettings());
                builder += chordTypeToString(spanAt(sequence, i).toType);
            } else {
                builder += at(roots, i + 1).toString();
            }

            positions.value.push(startPosition);
        }

        return builder;
    }
}
