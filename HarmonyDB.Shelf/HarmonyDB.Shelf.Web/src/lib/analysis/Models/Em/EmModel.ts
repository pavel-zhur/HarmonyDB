import { BinaryReader, BinaryWriter } from '../../../common/dotnet/BinaryIO';
import { ArgumentException } from '../../../common/dotnet/exceptions';
import { range } from '../../../common/dotnet/Linq';
import { toByte } from '../../../common/dotnet/Numbers';
import type { ISource, KnownTonality } from '../../HarmonyDB.Index.Analysis.Em/Models';
import { Constants, Loop, Scale, Song } from '../../HarmonyDB.Index.Analysis.Em/Models';

function toDictionaryById<T extends { id: string }>(items: Iterable<T>): Map<string, T> {
    const result = new Map<string, T>();
    for (const item of items) {
        if (result.has(item.id)) throw new ArgumentException(`An item with the same key has already been added. Key: ${item.id}`);
        result.set(item.id, item);
    }

    return result;
}

/**
 * The C# record also implements IEmModel explicitly, whose Songs and Loops (collections of the values) clash in name with
 * the public dictionaries; IEmModel is used only by the Em project's MusicAnalyzer, which is not ported, so it is not here.
 */
export class EmModel {
    private readonly _songs: Map<string, Song>;
    private readonly _loops: Map<string, Loop>;

    constructor(songs: Iterable<Song>, loops: Iterable<Loop>) {
        this._songs = toDictionaryById(songs);
        this._loops = toDictionaryById(loops);
    }

    get songs(): ReadonlyMap<string, Song> {
        return this._songs;
    }

    get loops(): ReadonlyMap<string, Loop> {
        return this._loops;
    }

    static deserialize(serialized: Uint8Array): EmModel {
        const reader = new BinaryReader(serialized);

        const loops = range(0, reader.readInt32())
            .map(() => {
                const id = reader.readString();
                const loop = new Loop({
                    id,
                });

                EmModel.deserializeSource(reader, loop);

                return loop;
            });

        const songs = range(0, reader.readInt32())
            .map(() => {
                const id = reader.readString();

                const knownTonalityByte = reader.readByte();
                const knownTonality: KnownTonality | null =
                    knownTonalityByte === 255 ? null : { tonic: toByte(Math.trunc(knownTonalityByte / 2)), scale: (knownTonalityByte % 2) as Scale };

                const song = new Song({
                    id,
                    knownTonality,
                });

                EmModel.deserializeSource(reader, song);

                return song;
            });

        return new EmModel(songs, loops);
    }

    serialize(): Uint8Array {
        const writer = new BinaryWriter();

        const songIds = new Map<string, number>();
        const loopIds = new Map<string, number>();

        writer.writeInt32(this.loops.size);
        for (const [key, loop] of this.loops) {
            loopIds.set(key, loopIds.size);
            writer.writeString(key);
            EmModel.serializeSource(writer, loop);
        }

        writer.writeInt32(this.songs.size);
        for (const [key, song] of this.songs) {
            songIds.set(key, songIds.size);
            writer.writeString(key);
            writer.writeByte(song.knownTonality !== null
                ? toByte(song.knownTonality.tonic * 2 + song.knownTonality.scale)
                : 255);

            EmModel.serializeSource(writer, song);
        }

        return writer.toArray();
    }

    private static serializeSource(writer: BinaryWriter, source: ISource): void {
        writer.writeSingle(source.score.tonicScore);
        writer.writeSingle(source.score.scaleScore);
        for (let i = 0; i < Constants.TonicCount; i++) {
            for (let j = 0; j < Constants.ScaleCount; j++) {
                writer.writeSingle(source.tonalityProbabilities[i]![j]!);
            }
        }
    }

    private static deserializeSource(reader: BinaryReader, source: ISource): void {
        const tonicScore = reader.readSingle();
        const scaleScore = reader.readSingle();
        source.score = { tonicScore, scaleScore };

        for (let i = 0; i < Constants.TonicCount; i++) {
            for (let j = 0; j < Constants.ScaleCount; j++) {
                source.tonalityProbabilities[i]![j] = reader.readSingle();
            }
        }
    }
}
