import { BinaryReader, BinaryWriter } from '../../../common/dotnet/BinaryIO';
import { Lookup } from '../../../common/dotnet/Collections';
import { ArgumentException, ArgumentOutOfRangeException } from '../../../common/dotnet/exceptions';
import { at, averageFloat, distinct, groupBy, orderBy, sumFloat } from '../../../common/dotnet/Linq';
import { compareInts } from '../../../common/dotnet/Numbers';
import { withIndices } from '../../../common/OneShelf.Common/Extensions';
import { deserializeLoop } from '../../Tools/LoopSerializationExtensions';
import { StructureLink } from './StructureLink';
import { StructureLoop } from './StructureLoop';
import { StructureSong } from './StructureSong';

/** ToDictionary with string keys: throws on a duplicate key. */
function toDictionary<T, V>(items: Iterable<T>, keySelector: (item: T) => string, valueSelector: (item: T) => V): Map<string, V> {
    const result = new Map<string, V>();
    for (const item of items) {
        const key = keySelector(item);
        if (result.has(key)) throw new ArgumentException(`An item with the same key has already been added. Key: ${key}`);
        result.set(key, valueSelector(item));
    }

    return result;
}

export class Structures {
    private constructor(links: readonly StructureLink[], loops: ReadonlyMap<string, StructureLoop>, songs: ReadonlyMap<string, StructureSong>) {
        this.links = links;
        this.loops = loops;
        this.songs = songs;
        this.linksBySongId = new Lookup(links.map(x => [x.externalId, x] as const));
        this.linksByLoopId = new Lookup(links.map(x => [x.normalized, x] as const));
    }

    private static fromLinks(links: StructureLink[]): Structures {
        return new Structures(
            links,
            toDictionary(
                groupBy(links, x => x.normalized).map(g => {
                    const sequence = deserializeLoop(g.key);
                    return new StructureLoop(
                        g.key,
                        sequence.length,
                        sumFloat(g.items.map(x => x.occurrences)),
                        sumFloat(g.items.map(x => x.successions)),
                        averageFloat(g.items.map(x => x.coverage)),
                        distinct(g.items.map(x => x.externalId)).length);
                }),
                x => x.normalized,
                x => x),
            toDictionary(
                groupBy(links, x => x.externalId)
                    .map(x => new StructureSong(x.key, distinct(x.items.map(x => x.normalized)).length)),
                x => x.externalId,
                x => x));
    }

    readonly links: readonly StructureLink[];

    readonly loops: ReadonlyMap<string, StructureLoop>;

    readonly songs: ReadonlyMap<string, StructureSong>;

    readonly linksBySongId: Lookup<string, StructureLink>;

    readonly linksByLoopId: Lookup<string, StructureLink>;

    reduce(takeOnly: (link: StructureLink) => boolean): Structures {
        const links = this.links.filter(takeOnly);
        const loopIds = new Set(links.map(x => x.normalized));
        const songIds = new Set(links.map(x => x.externalId));
        return new Structures(
            links,
            toDictionary([...this.loops].filter(p => loopIds.has(p[0])), x => x[0], x => x[1]),
            toDictionary([...this.songs].filter(p => songIds.has(p[0])), x => x[0], x => x[1]));
    }

    static serialize(compactLinks: readonly StructureLink[]): Uint8Array {
        const binaryWriter = new BinaryWriter();

        const normalized = toDictionary(withIndices(distinct(compactLinks.map(x => x.normalized))), x => x.x, x => x.i);
        const externalIds = toDictionary(withIndices(distinct(compactLinks.map(x => x.externalId))), x => x.x, x => x.i);

        binaryWriter.writeInt32(normalized.size);
        for (const pair of orderBy(normalized, (a, b) => compareInts(a[1], b[1]))) {
            binaryWriter.writeString(pair[0]);
        }

        binaryWriter.writeInt32(externalIds.size);
        for (const pair of orderBy(externalIds, (a, b) => compareInts(a[1], b[1]))) {
            binaryWriter.writeString(pair[0]);
        }

        binaryWriter.writeInt32(compactLinks.length);
        for (const link of compactLinks) {
            binaryWriter.writeInt32(normalized.get(link.normalized)!);
            binaryWriter.writeInt32(externalIds.get(link.externalId)!);
            binaryWriter.writeByte(link.normalizationRoot);
            binaryWriter.writeSingle(link.occurrences);
            binaryWriter.writeSingle(link.successions);
            binaryWriter.writeSingle(link.coverage);
        }

        return binaryWriter.toArray();
    }

    static deserialize(data: Uint8Array): Structures {
        const binaryReader = new BinaryReader(data);

        const normalizedCount = binaryReader.readInt32();
        if (normalizedCount < 0) throw new ArgumentOutOfRangeException(undefined, 'capacity'); // new List<T>(capacity)
        const normalized: string[] = [];
        for (let i = 0; i < normalizedCount; i++) {
            normalized.push(binaryReader.readString());
        }

        const externalIdsCount = binaryReader.readInt32();
        if (externalIdsCount < 0) throw new ArgumentOutOfRangeException(undefined, 'capacity'); // new List<T>(capacity)
        const externalIds: string[] = [];
        for (let i = 0; i < externalIdsCount; i++) {
            externalIds.push(binaryReader.readString());
        }

        const linksCount = binaryReader.readInt32();
        if (linksCount < 0) throw new ArgumentOutOfRangeException(undefined, 'capacity'); // new List<T>(capacity)
        const links: StructureLink[] = [];
        for (let i = 0; i < linksCount; i++) {
            const normalizedIndex = binaryReader.readInt32();
            const externalIdIndex = binaryReader.readInt32();
            const normalizationRoot = binaryReader.readByte();
            const occurrences = binaryReader.readSingle();
            const successions = binaryReader.readSingle();
            const coverage = binaryReader.readSingle();

            const link = new StructureLink(
                at(normalized, normalizedIndex),
                at(externalIds, externalIdIndex),
                normalizationRoot,
                occurrences,
                successions,
                coverage);

            links.push(link);
        }

        return Structures.fromLinks(links);
    }
}
