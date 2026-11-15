// Runs a case through the TypeScript ports and returns what they give, in the neutral form that
// tools/dotnet-reference/DotnetReference (the real C# libraries) writes for the same case. Each function here mirrors
// the one of the same name in DotnetReference/Suites*.cs: the same calls, in the same order, projected the same way.

import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { transposition } from '../common/HarmonyDB.Common/Transposition/TranspositionExtensions';
import { Separators, searchSyntaxAnySeparatorsToRemove, searchSyntaxRemoveSeparators, toSearchSyntax } from '../common/HarmonyDB.Common.FullTextSearch/FullTextSearchExtensions';
import { getFragments } from '../common/HarmonyDB.Common.FullTextSearch/SearchHighlightingTools';
import type { ILike } from '../common/OneShelf.Common.Songs/ILike';
import { buildCache, find } from '../common/OneShelf.Common.Songs/FullTextSearch/FullTextSearchLogic';
import type { ISearchableArtist } from '../common/OneShelf.Common.Songs/FullTextSearch/ISearchableArtist';
import type { ISearchableSong } from '../common/OneShelf.Common.Songs/FullTextSearch/ISearchableSong';
import { calculateHash } from '../common/OneShelf.Common.Songs/Hashing/MyHashExtensions';
import { getRating } from '../common/OneShelf.Common.Songs/SongsConstants';
import { once, withIndices } from '../common/OneShelf.Common/Extensions';
import { fromBase64String, toBase64String } from '../common/dotnet/Convert';
import { DateTime } from '../common/dotnet/DateTime';
import { enumParse, enumToString } from '../common/dotnet/Enum';
import { int64HashCode, singleHashCode } from '../common/dotnet/hashCode';
import { htmlAttributeEncode, htmlEncode } from '../common/dotnet/html';
import { orderBy, compareStrings } from '../common/dotnet/Linq';
import { listSort } from '../common/dotnet/ListSort';
import { byteParse, compareInts, formatN, formatP, toFloat } from '../common/dotnet/Numbers';
import { Out } from '../common/dotnet/Out';
import { PriorityQueue } from '../common/dotnet/PriorityQueue';
import {
    containsIgnoreCase, isDigit, isNullOrWhiteSpace, isUpper, isWhiteSpace, replaceIgnoreCase, split, startsWithIgnoreCase,
    StringSplitOptions, toLowerInvariant, toLowerInvariantChar, toUpperInvariant, trim, tryParseInt32,
} from '../common/dotnet/text';
import { camelCaseJsonSerializerOptions, defaultJsonSerializerOptions, deserialize, jsonByte, jsonDateTime, jsonDouble, jsonGuid, jsonInt32, jsonInt64, jsonNullable, jsonSingle, jsonString, serialize, toCamelCase, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { writeJsonDouble, writeJsonSingle, writeJsonString } from '../common/dotnet/json/JsonWriter';
import type { ILogger } from '../common/dotnet/Logging';
import { NoteAlterationJson } from '../common/HarmonyDB.Common/NoteAlteration';
import { NodeHtml, Note, RepresentationSettings, type SimplificationMode } from '../representations';
import type { ChordsProgression } from '../analysis/Models/ChordsProgression';
import type { ChordType } from '../analysis/Models/ChordType';
import { CompactChordsProgression } from '../analysis/Models/CompactV1/CompactChordsProgression';
import { CompactHarmonyMovement } from '../analysis/Models/CompactV1/CompactHarmonyMovement';
import { CompactHarmonyMovementsSequence } from '../analysis/Models/CompactV1/CompactHarmonyMovementsSequence';
import type { HarmonyGroup } from '../analysis/Models/HarmonyGroup';
import type { HarmonyMovementsSequence } from '../analysis/Models/HarmonyMovementsSequence';
import { EdgeBlock } from '../analysis/Models/Index/Blocks/EdgeBlock';
import type { IBlock } from '../analysis/Models/Index/Blocks/Interfaces/IBlock';
import { isIIndexedBlock } from '../analysis/Models/Index/Blocks/Interfaces/IIndexedBlock';
import { LoopBlock } from '../analysis/Models/Index/Blocks/LoopBlock';
import { LoopBlockBase } from '../analysis/Models/Index/Blocks/LoopBlockBase';
import { LoopMassiveOverlapsBlock } from '../analysis/Models/Index/Blocks/LoopMassiveOverlapsBlock';
import { LoopSelfJumpBlock } from '../analysis/Models/Index/Blocks/LoopSelfJumpBlock';
import { LoopSelfMultiJumpBlock } from '../analysis/Models/Index/Blocks/LoopSelfMultiJumpBlock';
import { PingPongBlock } from '../analysis/Models/Index/Blocks/PingPongBlock';
import { PolyLoopBlock } from '../analysis/Models/Index/Blocks/PolyLoopBlock';
import { PolySequenceBlock } from '../analysis/Models/Index/Blocks/PolySequenceBlock';
import { RoundRobinBlock } from '../analysis/Models/Index/Blocks/RoundRobinBlock';
import { SequenceBlock } from '../analysis/Models/Index/Blocks/SequenceBlock';
import type { BlockType } from '../analysis/Models/Index/Enums/BlockType';
import type { BlockGraph } from '../analysis/Models/Index/Graphs/BlockGraph';
import type { IBlockJoint } from '../analysis/Models/Index/Graphs/Interfaces/IBlockJoint';
import { PolyBlocksExtractionParameters } from '../analysis/Models/Index/PolyBlocksExtractionParameters';
import type { StructureLink } from '../analysis/Models/Structure/StructureLink';
import { BlocksChartParameters } from '../analysis/Models/TextGraphics/BlocksChartParameters';
import type { HarmonyDataV1 } from '../analysis/Models/V1/HarmonyDataV1';
import type { ChordDataV1 } from '../analysis/Models/V1/ChordDataV1';
import { CompressedChordsProgressionDataV1 } from '../analysis/Models/V1/CompressedChordsProgressionDataV1';
import { ChordDataParser } from '../analysis/Services/ChordDataParser';
import { IndexExtractor } from '../analysis/Services/IndexExtractor';
import { InputParser } from '../analysis/Services/InputParser';
import { PathsSearcher } from '../analysis/Services/PathsSearcher';
import { ProgressionsBuilder } from '../analysis/Services/ProgressionsBuilder';
import { ProgressionsSearch, type SearchResult as SearchResultOf } from '../analysis/Services/ProgressionsSearch';
import { ProgressionsVisualizer } from '../analysis/Services/ProgressionsVisualizer';
import { deserializeLoop, serializeLoop } from '../analysis/Tools/LoopSerializationExtensions';
import {
    fromIndex, getAmCRoot, getAmCTonic, getFunctionsTitle, getLoopTonalityShiftsDisplayOrder, getMajorTonic, getPredictedTonality,
    getRelativeScale, getSecondPredictedTonality, getTitle, getTitleFromBeginningNote, getTitleOfSequence,
    getTitleWithPredictedAndBeginningNote, getWeight, toChord, toFunction, toIndexOfTuple, toLinear, toLoopTonalityShiftTitle,
    toLoopTonalityTitle, toLoopTonalityTitleOfIndex, toLoopTonalityTitleWithNormalizationRoot, toLoopTonalityTitleWithPredicted,
    tonalityConfidence, tonicConfidence, toSequence, toSongTonalityTitle, toSongTonalityTitleOfIndex, tryParseBestTonality, type Tonality,
} from '../analysis/Tools/TonalitiesExtensions';
import { modelContracts } from './modelContracts';
import { uriSuite } from './uriSuite';

/* eslint-disable @typescript-eslint/no-explicit-any */
type Input = any;
type Output = unknown;

// The .NET type names of the errors the ports throw; a plain C# `throw new("...")` (System.Exception) is a plain Error.
export function errorName(e: unknown): string {
    // A TypeError is what JavaScript throws where .NET throws a NullReferenceException (a member of null).
    if (e instanceof Error) return e.name === 'Error' ? 'Exception' : e.name === 'TypeError' ? 'NullReferenceException' : e.name;
    return typeof e;
}

const D = (value: number): number | string => Number.isFinite(value) ? value : Number.isNaN(value) ? 'NaN' : value > 0 ? 'Infinity' : '-Infinity';

function tryRun(func: () => Output): Output {
    try {
        return func();
    } catch (e) {
        return { $error: errorName(e) };
    }
}

const dbl = (value: number | string): number => typeof value === 'number' ? value : value === 'NaN' ? NaN : value === 'Infinity' ? Infinity : value === '-Infinity' ? -Infinity : value === '-0' ? -0 : NaN;

const nullLogger: ILogger = { logInformation: () => {}, logWarning: () => {}, logError: () => {} };

// One instance of each service for the whole run, as the site's singletons.
const chordDataParser = new ChordDataParser(nullLogger);
const progressionsBuilder = new ProgressionsBuilder(nullLogger);
const progressionsSearch = new ProgressionsSearch(nullLogger);
const progressionsVisualizer = new ProgressionsVisualizer();
const pathsSearcher = new PathsSearcher();
const indexExtractor = new IndexExtractor(pathsSearcher);
const inputParser = new InputParser(progressionsBuilder, chordDataParser);

export function runCase(suite: string, input: Input): Output {
    return tryRun(() => {
        switch (suite) {
            case 'chord': return chord(input);
            case 'note': return noteSuite(input);
            case 'html': return html(input);
            case 'search': return search(input);
            case 'loop': return loop(input);
            case 'links': return links(input);
            case 'tonal': return tonal(input);
            case 'fts': return fts(input);
            case 'dotnet': return dotnet(input);
            case 'json': return json(input);
            case 'uri': return uriSuite(input, tryRun);
            default: throw new Error(`Unknown suite ${suite}.`);
        }
    });
}

// Projections.

const harmonyData = (x: HarmonyDataV1 | null): Output => x === null ? null : [x.root, x.alteration, x.chordType];

const chordData = (x: ChordDataV1): Output => [x.originalRepresentation, x.harmonyRepresentation, harmonyData(x.harmonyData), x.chordData];

const movements = (items: readonly CompactHarmonyMovement[]): Output => items.map(m => [m.rootDelta, m.fromType, m.toType]);

const sequence = (s: HarmonyMovementsSequence, groups: readonly HarmonyGroup[]): Output => ({
    fr: s.firstRoot,
    ffi: tryRun(() => s.firstMovementFromIndex),
    m: s.movements.map(m => [m.rootDelta, m.fromType, m.toType, m.title, groups.indexOf(m.from), groups.indexOf(m.to)]),
});

function progression(p: ChordsProgression): Output {
    const groups = p.harmonySequence.map(x => x.harmonyGroup);
    const standard = p.standardHarmonyMovementsSequences;
    const extended = p.extendedHarmonyMovementsSequences;
    const index = (s: HarmonyMovementsSequence | null, list: readonly HarmonyMovementsSequence[] | null) => s === null ? null : list === null ? -2 : list.indexOf(s);
    return {
        h: p.harmonySequence.map(x => [
            x.harmonyGroup.index,
            x.harmonyGroup.startChordIndex,
            x.harmonyGroup.endChordIndex,
            x.harmonyGroup.harmonyRepresentation,
            harmonyData(x.harmonyGroup.harmonyData),
            x.harmonyGroup.isStandard,
            x.harmonyGroup.originalSequence.map(c => c.chordData),
            index(x.standardMovementsSequence, standard),
            index(x.extendedMovementsSequence, extended),
        ]),
        o: p.originalSequence.map(x => [
            x.original.chordData,
            groups.indexOf(x.harmonyGroup),
            x.indexInHarmonyGroup,
            index(x.standardMovementsSequence, standard),
            index(x.extendedMovementsSequence, extended),
        ]),
        s: standard === null ? null : standard.map(s => sequence(s, groups)),
        e: extended.map(s => sequence(s, groups)),
    };
}

function searchResult(progressions: readonly ChordsProgression[], result: SearchResultOf): Output {
    return {
        f: [...result.foundProgressionsWithCoverage].map(([key, value]) => [progressions.indexOf(key), D(value)]),
        g: [...result.harmonyGroupsWithIsFirst].map(([key, value]) => {
            const i = progressions.findIndex(p => p.harmonySequence.some(g => g.harmonyGroup === key));
            return [i, progressions[i]!.harmonySequence.map(g => g.harmonyGroup).indexOf(key), value];
        }),
    };
}

const customAttributes = (attributes: ReadonlyMap<number, string>): Output => [...attributes].map(([key, value]) => [key, value]);

const structureLink = (link: StructureLink): Output => ({
    n: link.normalized,
    x: link.externalId,
    r: link.normalizationRoot,
    o: D(link.occurrences),
    s: D(link.successions),
    c: D(link.coverage),
    st: tryRun(() => progressionsVisualizer.getLoopStatisticsTitle(link)),
    ct: tryRun(() => progressionsVisualizer.getLoopChordsTitle(link)),
});

// Reading the input.

export function settingsOf(e: Input): RepresentationSettings {
    const ca = e.ca != null ? new Map<number, string>((e.ca as [number, string][]).map(([k, v]) => [k, v])) : null;
    let settings = new RepresentationSettings({
        transpose: e.t,
        alteration: e.a ?? null,
        testOnlyX: e.x ?? false,
        isVariableWidth: e.vw ?? false,
        simplification: e.s as SimplificationMode,
        customAttributes: ca,
    });
    if (e.rel != null) {
        settings = settings.with({ relativeTo: { note: e.rel[0], major: e.rel[1] } });
    }

    for (const op of (e.ops ?? '') as string) {
        settings = op === 'u' ? settings.up() : settings.down();
    }

    return settings;
}

const readMovements = (e: [number, number, number][]): CompactHarmonyMovement[] =>
    e.map(m => new CompactHarmonyMovement({ rootDelta: m[0], fromType: m[1] as ChordType, toType: m[2] as ChordType }));

// Suites.

function chord(input: Input): Output {
    const s: string = input.s;
    return {
        get: tryRun(() => {
            const x = chordDataParser.getChord(s);
            return x === null ? null : {
                chord: x.chord,
                root: x.root,
                bass: x.bass,
                cf: x.chordTypeForCircleOfFifths,
                cp: x.chordTypeForProgressions,
                fing: x.fingering,
                ra: x.rootAlteration,
                ba: x.bassAlteration,
            };
        }),
        notes: tryRun(() => {
            const x = chordDataParser.getNotes(s);
            return x === null ? null : { bass: x.bass, main: x.main, root: x.root, fing: x.fingering };
        }),
        data: tryRun(() => chordData(chordDataParser.getProgressionData(s))),
        input: tryRun(() => chordData(inputParser.parseChord(s))),
    };
}

function noteSuite(input: Input): Output {
    const note = new Note(input.v, input.a ?? null);
    const settings = settingsOf(input.st);
    const delta: number = input.d;
    const noteOut = (n: Note) => [n.value, n.alteration];
    return {
        rep: tryRun(() => note.representation(settings)),
        tone: tryRun(() => note.tone(settings)),
        len: note.length(),
        elen: tryRun(() => note.estimatedLength(settings)),
        sharp: tryRun(() => noteOut(note.sharp())),
        flat: tryRun(() => noteOut(note.flat())),
        add: tryRun(() => noteOut(note.add(delta))),
        norm: Note.normalize(delta),
        settings: { t: settings.transpose, a: settings.alteration, s: settings.simplification },
        cof: Note.CircleOfFifths.map(x => [noteOut(x.major), noteOut(x.minor), x.isSharps]),
    };
}

function html(input: Input): Output {
    const text: string = input.json;
    const read = () => deserialize(text, NodeHtml.json, defaultJsonSerializerOptions);
    const settingsList = ((input.st ?? []) as Input[]).map(settingsOf);
    const result: Record<string, Output> = {
        lyrics: tryRun(() => read().asLyrics()),
        lyricsRaw: tryRun(() => read().asLyrics(false)),
        counts: tryRun(() => [read().getChordsCount(), read().getNodesCount()]),
        compress: tryRun(() => {
            const htmlNode = read();
            htmlNode.fix0_Compress();
            return serialize(htmlNode, NodeHtml.json, defaultJsonSerializerOptions);
        }),
        per: settingsList.map(settings => ({
            html: tryRun(() => read().asHtml(settings)),
            chords: tryRun(() => read().asChords(settings)),
            oneShelf: tryRun(() => read().asOneShelf(settings)),
            all: tryRun(() => read().getAllChords(settings).map(c => c.representation(settings))),
        })),
    };

    if (input.pipeline) {
        const settings = settingsList[0]!;
        result.pipeline = tryRun(() => pipeline(read(), settings, ((input.ex ?? []) as [number, number][]).map(x => ({ start: x[0], end: x[1] }))));
    }

    return result;
}

// What the site does with a song's chords (ChordsView, ProgressionsCacheLoader, DataProvider).
function pipeline(htmlNode: NodeHtml, settings: RepresentationSettings, extracts: { start: number; end: number }[]): Output {
    const result: Record<string, Output> = {};
    const chords = htmlNode.asChords(settings);
    const data = chords.map(x => chordDataParser.getProgressionData(x));
    result.data = data.map(chordData);
    const p = progressionsBuilder.buildProgression(data);
    result.progression = progression(p);
    result.progressionStd = tryRun(() => progression(progressionsBuilder.buildProgression(data, true)));
    result.compressed = tryRun(() => {
        const compressed = CompressedChordsProgressionDataV1.compress(data);
        const decompressed = compressed.decompress();
        return {
            seq: compressed.sequence,
            distinct: compressed.distinct.map(chordData),
            same: decompressed.length === data.length && decompressed.every((x, i) => x.equalityKey() === data[i]!.equalityKey()),
        };
    });
    const compact = p.compact();
    result.compact = tryRun(() => toBase64String(compact.serialize()));
    result.compactBack = tryRun(() => toBase64String(CompactChordsProgression.deserialize(compact.serialize()).serialize()));

    const links = indexExtractor.findStructureLinks('does not matter', compact);
    result.links = links.map(link => ({
        link: structureLink(link),
        search: tryRun(() => {
            const searchProgression = inputParser.parseSequence(getTitleFromBeginningNote(link.normalized, 0, true));
            const found = progressionsSearch.search(once(p), searchProgression);
            const attributes = progressionsVisualizer.buildCustomAttributesForSearch(p, found);
            return {
                phrase: movements(searchProgression.compact().movements),
                found: searchResult([p], found),
                attributes: customAttributes(attributes),
                html: tryRun(() => htmlNode.asHtml(settings.with({ customAttributes: attributes }))),
            };
        }),
    }));
    result.sortedLinks = tryRun(() => orderBy(withIndices(links), (a, b) => compareInts(deserializeLoop(a.x.normalized).length, deserializeLoop(b.x.normalized).length)).map(x => x.i));

    result.extracts = extracts.map(x => tryRun(() => {
        const searchPhrase = progressionsSearch.extractSearch(p, x.start, x.end);
        if (searchPhrase === null) return null;
        const groups = p.harmonySequence.map(g => g.harmonyGroup);
        const found = progressionsSearch.search(once(p), searchPhrase);
        const attributes = progressionsVisualizer.buildCustomAttributesForSearch(p, found);
        return {
            search: sequence(searchPhrase, groups),
            found: searchResult([p], found),
            attributes: customAttributes(attributes),
            html: tryRun(() => htmlNode.asHtml(new RepresentationSettings({
                transpose: settings.transpose,
                alteration: settings.alteration,
                testOnlyX: settings.testOnlyX,
                isVariableWidth: settings.isVariableWidth,
                simplification: settings.simplification,
                customAttributes: attributes,
            }))),
        };
    }));

    return result;
}

function search(input: Input): Output {
    const progressions = ((input.songs ?? []) as string[][])
        .map(song => progressionsBuilder.buildProgression(song.map(c => chordDataParser.getProgressionData(c))));
    if (input.dup && progressions.length > 0) progressions.push(progressions[0]!);
    const compacts = progressions.map(p => p.compact());
    const top: number | null = input.top ?? null;

    return {
        progressions: progressions.map(progression),
        results: ((input.ex ?? []) as [number, number, number][]).map(x => tryRun(() => {
            const p = progressions[x[0]]!;
            const searchPhrase = progressionsSearch.extractSearch(p, x[1], x[2]);
            if (searchPhrase === null) return null;
            const groups = p.harmonySequence.map(g => g.harmonyGroup);
            return {
                search: sequence(searchPhrase, groups),
                found: tryRun(() => searchResult(progressions, progressionsSearch.search(progressions, searchPhrase))),
                compact: tryRun(() => [...progressionsSearch.searchSearchable(compacts, searchPhrase, top)].map(([key, value]) => [compacts.indexOf(key as CompactChordsProgression), D(value)])),
            };
        })),
    };
}

/** GetType().Name of a block. */
function blockTypeName(b: IBlock): string {
    if (b instanceof LoopBlock) return 'LoopBlock';
    if (b instanceof PolyLoopBlock) return 'PolyLoopBlock';
    if (b instanceof SequenceBlock) return 'SequenceBlock';
    if (b instanceof PolySequenceBlock) return 'PolySequenceBlock';
    if (b instanceof PingPongBlock) return 'PingPongBlock';
    if (b instanceof RoundRobinBlock) return 'RoundRobinBlock';
    if (b instanceof EdgeBlock) return 'EdgeBlock';
    if (b instanceof LoopSelfJumpBlock) return 'LoopSelfJumpBlock';
    if (b instanceof LoopSelfMultiJumpBlock) return 'LoopSelfMultiJumpBlock';
    if (b instanceof LoopMassiveOverlapsBlock) return 'LoopMassiveOverlapsBlock';
    return '?';
}

function block(b: IBlock, all: readonly IBlock[]): Output {
    const result: Record<string, Output> = {
        k: blockTypeName(b),
        t: b.type,
        s: b.startIndex,
        e: b.endIndex,
        l: b.blockLength,
    };
    if (isIIndexedBlock(b)) {
        result.n = b.normalized;
        result.nr = b.normalizationRoot;
        result.ch = b.children.map(c => [all.indexOf(c), c.type, c.startIndex, c.endIndex]);
        const coordinates: Output[] = [];
        for (let i = b.startIndex - 1; i < b.startIndex - 1 + Math.min(b.blockLength, 40) + 2; i++) {
            coordinates.push(tryRun(() => b.getNormalizedCoordinate(i)));
        }
        result.co = coordinates;
    }

    if (b instanceof LoopBlockBase) {
        result.loop = serializeLoop(b.loop);
        result.sh = b.normalizationShift;
        result.occ = D(b.occurrences);
        result.suc = D(b.successions);
    }

    return result;
}

function loop(input: Input): Output {
    const sequenceMovements = readMovements(input.m);
    const firstRoot: number = input.fr;
    const result: Record<string, Output> = {
        serialized: tryRun(() => serializeLoop(sequenceMovements)),
        normalized: tryRun(() => {
            const shift = new Out<number>();
            const invariants = new Out<number>();
            const normalized = indexExtractor.getNormalizedProgression(sequenceMovements, shift, invariants);
            return [serializeLoop(normalized), shift.value, invariants.value];
        }),
    };

    let roots: number[];
    try {
        roots = indexExtractor.createRoots(sequenceMovements, firstRoot);
        result.roots = roots;
    } catch (e) {
        result.roots = { $error: errorName(e) };
        return result;
    }

    result.loops = tryRun(() => {
        const loops = indexExtractor.findLoops(sequenceMovements, roots);
        return loops.map(l => block(l, loops));
    });

    for (const types of (input.types ?? []) as number[][]) {
        const blockTypes = types as BlockType[];
        const key = 'blocks' + blockTypes.join('_');
        result[key] = tryRun(() => {
            const blocks = indexExtractor.findBlocks(sequenceMovements, roots, blockTypes, input.pp ? new PolyBlocksExtractionParameters({
                sequencesExcludeIfContainsAllChordsMoreThanOnce: true,
                sequencesExcludeIfContainsSuccessiveLoopsBreak: true,
                loopsExcludeIfContainsAllChordsMoreThanOnce: true,
                loopsExcludeIfContainsSuccessiveLoopsBreak: true,
            }) : null);
            const output: Record<string, Output> = { blocks: blocks.map(b => block(b, blocks)) };
            let graph: BlockGraph;
            try {
                graph = indexExtractor.createGraph(blocks);
            } catch (e) {
                output.graph = { $error: errorName(e) };
                return output;
            }

            output.joints = graph.joints.map(j => [
                blocks.indexOf(j.block1.block),
                blocks.indexOf(j.block2.block),
                tryRun(() => j.overlapLength),
                tryRun(() => j.normalization),
                j.isEdge,
            ]);
            output.envs = [...graph.environments].map(([key, value]) => [
                blocks.indexOf(key),
                value.children.map(c => blocks.indexOf(c.block)),
                value.childrenSubtree.map(c => blocks.indexOf(c.block)),
                value.parents.map(c => blocks.indexOf(c.block)),
            ]);
            let path: IBlockJoint[];
            try {
                path = pathsSearcher.dijkstra(graph);
                output.path = path.map(j => graph.joints.indexOf(j));
            } catch (e) {
                output.path = { $error: errorName(e) };
                path = [];
            }

            for (const [name, parameters] of [
                ['v1', new BlocksChartParameters()],
                ['v2', new BlocksChartParameters({ typesToo: false, groupNormalized: false, showJoints: false, showPath: false })],
                ['v3', new BlocksChartParameters({ groupNormalized: false, showPath: false })],
            ] as const) {
                output[name] = tryRun(() => {
                    const one = progressionsVisualizer.visualizeBlocksAsOne(sequenceMovements, roots, blocks, graph, path, parameters);
                    const oneText = one.toString();
                    const { left, right } = progressionsVisualizer.visualizeBlocksAsTwo(sequenceMovements, roots, blocks, graph, path, parameters);
                    const leftText = left.toString();
                    return [oneText, one.asHtml(), leftText, right.toString(), left.asHtml()];
                });
            }

            return output;
        });
    }

    return result;
}

function links(input: Input): Output {
    const compact = new CompactChordsProgression({
        harmonySequenceLength: input.len,
        extendedHarmonyMovementsSequences: ((input.seqs ?? []) as Input[]).map(s => new CompactHarmonyMovementsSequence({
            firstRoot: s.fr,
            firstMovementFromIndex: s.fi,
            movements: readMovements(s.m),
        })),
    });

    return {
        serialized: tryRun(() => toBase64String(compact.serialize())),
        links: tryRun(() => indexExtractor.findStructureLinks(input.id ?? 'x', compact).map(structureLink)),
        compactSearch: tryRun(() => {
            const phrase = inputParser.parseSequence(input.phrase);
            return [...progressionsSearch.searchSearchable([compact], phrase, input.top ?? null)].map(([, value]) => D(value));
        }),
    };
}

const tonalityOut = (t: Tonality): Output => [t.root, t.isMinor];

const tonalityOf = (e: [number, boolean] | null | undefined): Tonality | null => e == null ? null : { root: e[0], isMinor: e[1] };

function tonal(input: Input): Output {
    const probabilities = Float32Array.from(((input.p ?? []) as (number | string)[]).map(dbl));
    const predicted = tonalityOf(input.pred);
    const tonality = tonalityOf(input.ton)!;
    const beginningNote: number = input.bn;
    const normalizationRoot: number | null = input.nr ?? null;
    return {
        pred: tryRun(() => {
            const c = new Out<number>();
            const t = getPredictedTonality(probabilities, c);
            return [tonalityOut(t), D(c.value)];
        }),
        second: tryRun(() => {
            const c = new Out<number>();
            const t = getSecondPredictedTonality(probabilities, c);
            return [tonalityOut(t), D(c.value)];
        }),
        conf: tryRun(() => D(tonalityConfidence(probabilities))),
        tonic: tryRun(() => [D(tonicConfidence(probabilities, true)), D(tonicConfidence(probabilities, false))]),
        linear: tryRun(() => {
            const array = Array.from({ length: 12 }, () => new Float32Array(2));
            for (let i = 0; i < 24 && i < probabilities.length; i++) array[Math.trunc(i / 2)]![i % 2] = probabilities[(i * 7) % probabilities.length]!;
            return [...toLinear(array)].map(D);
        }),
        scale: [
            getMajorTonic(tonality, true), getMajorTonic(tonality, false),
            tonalityOut(getRelativeScale(tonality, true)), tonalityOut(getRelativeScale(tonality, false)),
            getAmCTonic(tonality), tonalityOut(getAmCRoot(tonality)),
            toIndexOfTuple(tonality),
            toSongTonalityTitle(tonality),
            toLoopTonalityTitle(tonality),
            tryRun(() => toLoopTonalityTitleWithNormalizationRoot(tonality, normalizationRoot)),
            tryRun(() => toLoopTonalityTitleWithPredicted(tonality, predicted)),
        ],
        order: [...getLoopTonalityShiftsDisplayOrder(predicted)],
        shifts: Array.from({ length: 14 }, (_, x) => tryRun(() => toLoopTonalityShiftTitle(x))),
        indices: ((input.idx ?? []) as number[]).map(i => [
            tryRun(() => tonalityOut(fromIndex(i))),
            tryRun(() => toSongTonalityTitleOfIndex(i)),
            tryRun(() => toLoopTonalityTitleOfIndex(i, predicted)),
        ]),
        parsed: ((input.strs ?? []) as string[]).map(s => tryRun(() => {
            const t = tryParseBestTonality(s);
            return t === null ? null : tonalityOut(t);
        })),
        chords: ((input.chords ?? []) as [number, number][]).map(c => ({ note: c[0], chordType: c[1] as ChordType })).map(c => [
            tryRun(() => toChord(c)),
            tryRun(() => toFunction(c, { tonic: tonality.root, isMinor: tonality.isMinor })),
        ]),
        weights: ((input.w ?? []) as [number | string, number | string, number, boolean][]).map(w => tryRun(() => D(getWeight(toFloat(dbl(w[0])), toFloat(dbl(w[1])), w[2], w[3])))),
        titles: ((input.loops ?? []) as string[]).map(normalized => [
            tryRun(() => movements(deserializeLoop(normalized))),
            tryRun(() => getTitle(normalized, predicted)),
            tryRun(() => getTitle(normalized, predicted, true)),
            tryRun(() => getTitle(normalized, predicted, true, false)),
            tryRun(() => getTitleFromBeginningNote(normalized, beginningNote)),
            tryRun(() => getTitleFromBeginningNote(normalized, beginningNote, true)),
            tryRun(() => getTitleWithPredictedAndBeginningNote(normalized, tonality, beginningNote)),
            tryRun(() => getTitleWithPredictedAndBeginningNote(normalized, tonality, beginningNote, true)),
            tryRun(() => getFunctionsTitle(normalized, tonality)),
            tryRun(() => getFunctionsTitle(normalized, tonality, true)),
            tryRun(() => getFunctionsTitle(normalized, tonality, true, false)),
            tryRun(() => getTitleOfSequence(deserializeLoop(normalized), predicted, beginningNote, true, true)),
            tryRun(() => {
                const shift = new Out<number | null>();
                const items: Output[] = toSequence(deserializeLoop(normalized), predicted, beginningNote, shift, true).map(x => [x.note, x.chordType]);
                items.push(shift.value);
                return items;
            }),
            tryRun(() => serializeLoop(deserializeLoop(normalized))),
        ]),
    };
}

interface SearchableSong extends ISearchableSong {
    readonly artists: ISearchableArtist[];
}

function fts(input: Input): Output {
    const songs: SearchableSong[] = ((input.songs ?? []) as Input[]).map(s => ({
        title: s.title,
        additionalKeywords: s.kw ?? null,
        index: s.index,
        likes: ((s.likes ?? []) as [number, number][]).map((l): ILike => ({ level: l[1], userId: l[0] })),
        artists: ((s.artists ?? []) as Input[]).map((a): ISearchableArtist => ({ name: a.name, synonyms: (a.syn ?? []) as string[] })),
    }));

    const queries = (input.queries ?? []) as string[];
    const result: Record<string, Output> = {
        ratings: songs.map(s => tryRun(() => D(getRating(s.likes)))),
        fragments: ((input.frag ?? []) as [string | null, string][]).map(f => tryRun(() => getFragments(f[0], f[1]).map(x => [x.fragment, x.isHighlighted]))),
        syntax: queries.map(q => [toSearchSyntax(q), searchSyntaxRemoveSeparators(q), searchSyntaxAnySeparatorsToRemove(q)]),
    };

    let cache;
    try {
        cache = buildCache(songs, s => s.artists);
        result.cache = cache.map(x => [songs.indexOf(x.song), [...x.words]]);
    } catch (e) {
        result.cache = { $error: errorName(e) };
        return result;
    }

    result.find = queries.flatMap(q => ((input.users ?? []) as number[]).map(u => tryRun(() => {
        const { found, isUserSpecific } = find(cache, q, u);
        return [found.map(s => songs.indexOf(s)), isUserSpecific];
    })));

    return result;
}

function deserializeValue<T>(text: string, contract: JsonContract<T>): T {
    return deserialize(text, contract, defaultJsonSerializerOptions);
}

function hex(bytes: Uint8Array): string {
    return [...bytes].map(b => b.toString(16).toUpperCase().padStart(2, '0')).join('');
}

function dotnet(input: Input): Output {
    switch (input.f as string) {
        case 'sort': {
            const items = ((input.keys ?? []) as number[]).map((key, i) => ({ key, i }));
            listSort(items, (a, b) => compareInts(a.key, b.key));
            return items.map(x => x.i);
        }
        case 'sortStrings': {
            const items = ((input.keys ?? []) as string[]).map((key, i) => ({ key, i }));
            const byDefault = [...items];
            listSort(byDefault, (a, b) => compareStrings(a.key, b.key));
            const ordered = orderBy(items, (a, b) => compareStrings(a.key, b.key));
            return [byDefault.map(x => x.i), ordered.map(x => x.i)];
        }
        case 'pq': {
            const queue = new PriorityQueue<number, number>(compareInts);
            const result: Output[] = [];
            let next = 0;
            for (const op of (input.ops ?? []) as (number | string)[]) {
                if (typeof op === 'number') {
                    queue.enqueue(next++, op);
                } else {
                    result.push(tryRun(() => queue.dequeue()));
                }
            }

            while (queue.count > 0) result.push(queue.dequeue());
            return result;
        }
        case 'pqFloat': {
            const queue = new PriorityQueue<number, number>((a, b) => a < b ? -1 : a > b ? 1 : a === b ? 0 : Number.isNaN(a) ? (Number.isNaN(b) ? 0 : -1) : 1);
            const result: Output[] = [];
            let next = 0;
            for (const op of (input.ops ?? []) as ([number | string] | string)[]) {
                if (Array.isArray(op)) {
                    queue.enqueue(next++, toFloat(dbl(op[0])));
                } else {
                    result.push(tryRun(() => queue.dequeue()));
                }
            }

            while (queue.count > 0) result.push(queue.dequeue());
            return result;
        }
        case 'format': {
            const value = dbl(input.v);
            const single = toFloat(value);
            const decimals: number = input.d;
            return [
                formatN(value, decimals),
                formatP(value, decimals),
                formatN(single, decimals),
                formatP(single, decimals),
                D(single),
                tryRun(() => writeJsonDouble(value)),
                tryRun(() => writeJsonSingle(single)),
                singleHashCode(single),
                Math.abs(value) < 4e18 ? int64HashCode(Math.trunc(value)) : null,
            ];
        }
        case 'readNumber': {
            const text: string = input.txt;
            return [
                tryRun(() => D(deserializeValue(text, jsonDouble))),
                tryRun(() => D(deserializeValue(text, jsonSingle))),
                tryRun(() => deserializeValue(text, jsonInt32)),
                tryRun(() => deserializeValue(text, jsonInt64)),
                tryRun(() => deserializeValue(text, jsonByte)),
                tryRun(() => {
                    const value = deserializeValue(text, jsonNullable(NoteAlterationJson));
                    return value === null ? null : enumToString(NoteAlteration, value);
                }),
            ];
        }
        case 'chars': {
            const ws: number[] = [], up: number[] = [], dg: number[] = [], rd: number[] = [], notAny: number[] = [];
            const lo: [number, number][] = [], tu: [number, number][] = [];
            const digitPattern = /^\p{Nd}$/u;
            const anyPattern = /^[^\n]$/u;
            for (let i = 0; i < 0x10000; i++) {
                const c = String.fromCharCode(i);
                if (isWhiteSpace(c)) ws.push(i);
                if (isUpper(c)) up.push(i);
                if (isDigit(c)) dg.push(i);
                const lower = toLowerInvariantChar(c);
                if (lower !== c) lo.push([i, lower.charCodeAt(0)]);
                const upper = toUpperInvariant(c);
                if (upper !== c) tu.push([i, upper.charCodeAt(0)]);
                if (digitPattern.test(c)) rd.push(i);
                if (!anyPattern.test(c)) notAny.push(i);
            }

            const slo: [number, number][] = [], stu: [number, number][] = [];
            for (let cp = 0; cp <= 0x10ffff; cp++) {
                if (cp >= 0xd800 && cp <= 0xdfff) continue;
                const s = String.fromCodePoint(cp);
                const lower = toLowerInvariant(s);
                if (lower !== s) slo.push([cp, lower.codePointAt(0)!]);
                const upper = toUpperInvariant(s);
                if (upper !== s) stu.push([cp, upper.codePointAt(0)!]);
            }

            return { ws, up, dg, lo, tu, rd, notAny, slo, stu };
        }
        case 'str': {
            const s: string = input.s;
            const t: string = input.t ?? '';
            return {
                trim: trim(s),
                ws: isNullOrWhiteSpace(s),
                lower: toLowerInvariant(s),
                upper: toUpperInvariant(s),
                split0: split(s, Separators),
                split1: split(s, Separators, StringSplitOptions.RemoveEmptyEntries),
                split3: split(s, Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                syntax: toSearchSyntax(s),
                json: writeJsonString(s),
                jsonBack: tryRun(() => deserializeValue(writeJsonString(s), jsonString)),
                camel: toCamelCase(s),
                int: tryRun(() => tryParseInt32(s)),
                intTrim: tryRun(() => tryParseInt32(trim(s))),
                byte: tryRun(() => byteParse(s)),
                enum: tryRun(() => enumToString(NoteAlteration, enumParse<NoteAlteration>(NoteAlteration, s))),
                html: htmlEncode(s),
                attr: htmlAttributeEncode(s),
                startsWith: s.startsWith(t),
                contains: s.includes(t),
                indexOf: s.indexOf(t),
                compare: Math.sign(compareStrings(s, t)),
                compareOrdinal: Math.sign(compareStrings(s, t)),
                containsIc: containsIgnoreCase(s, t),
                startsWithIc: startsWithIgnoreCase(s, t),
                replaceIc: tryRun(() => replaceIgnoreCase(s, t, '<>')),
                transposition: tryRun(() => {
                    const v = tryParseInt32(s);
                    return v === null ? null : transposition(v);
                }),
                b64: tryRun(() => hex(fromBase64String(s))),
                b64loop: tryRun(() => serializeLoop(deserializeLoop(s))),
                guid: tryRun(() => deserializeValue(writeJsonString(s), jsonGuid)),
                date: tryRun(() => {
                    const date = deserializeValue(writeJsonString(s), jsonDateTime);
                    const write = (value: DateTime) => serialize(value, jsonDateTime, defaultJsonSerializerOptions);
                    return [date.ticks.toString(), date.kind, write(date), write(date.toUniversalTime()), write(date.toLocalTime()), date.getHashCode()];
                }),
            };
        }
        case 'bytes': {
            const bytes = Uint8Array.from((input.b ?? []) as number[]);
            return [toBase64String(bytes), serialize(bytes, jsonBytesContract(), defaultJsonSerializerOptions)];
        }
        case 'hash': {
            const values = (input.v ?? []) as number[];
            return [calculateHash(values), values.map(transposition)];
        }
        default:
            throw new Error(`Unknown function ${input.f}.`);
    }
}

function jsonBytesContract() {
    return modelContracts.bytes;
}

function json(input: Input): Output {
    const contract = modelContracts.types.get(input.type);
    if (contract === undefined) throw new Error(`No contract for ${input.type}.`);
    const options = input.naming === 'camel' ? camelCaseJsonSerializerOptions : defaultJsonSerializerOptions;
    const value = deserialize(input.json, contract, options);
    return [serialize(value, contract, camelCaseJsonSerializerOptions), serialize(value, contract, defaultJsonSerializerOptions)];
}

