import { stringHashCode } from '../../../../common/dotnet/hashCode';
import { jsonClass, jsonInt32, jsonList, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import type { ISearchableArtist } from '../../../../common/OneShelf.Common.Songs/FullTextSearch/ISearchableArtist';

export interface ArtistInit {
    id: number;
    name: string;
    synonyms: readonly string[];
}

export class Artist implements ISearchableArtist {
    readonly id: number;
    readonly name: string;
    readonly synonyms: readonly string[];

    constructor(init: ArtistInit) {
        this.id = init.id;
        this.name = init.name;
        this.synonyms = init.synonyms;
    }

    deepHash(): number[] {
        return [
            this.id,
            stringHashCode(this.name),
            ...this.synonyms.map(x => stringHashCode(x)),
        ];
    }

    static readonly json: JsonContract<Artist> = jsonClass<Artist>('OneShelf.Frontend.Api.Model.V3.Databasish.Artist', init => new Artist(init as unknown as ArtistInit), () => [
        jsonProperty('Id', jsonInt32, { required: true }),
        jsonProperty('Name', jsonString, { required: true }),
        jsonProperty('Synonyms', jsonList(jsonString), { required: true }),
    ]);
}
