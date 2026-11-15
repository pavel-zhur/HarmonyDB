import { jsonClass, jsonDictionary, jsonInt32, jsonList, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { SongIllustrations } from './SongIllustrations';

export interface AllIllustrationsInit {
    songs: ReadonlyMap<number, SongIllustrations>;
    underGeneration?: readonly number[] | null;
    etag: number;
}

export class AllIllustrations {
    readonly songs: ReadonlyMap<number, SongIllustrations>;
    readonly underGeneration: readonly number[] | null;
    etag: number;

    constructor(init: AllIllustrationsInit) {
        this.songs = init.songs;
        this.underGeneration = init.underGeneration ?? null;
        this.etag = init.etag;
    }

    static readonly json: JsonContract<AllIllustrations> = jsonClass<AllIllustrations>('OneShelf.Frontend.Api.Model.V3.Illustrations.AllIllustrations', init => new AllIllustrations(init as unknown as AllIllustrationsInit), () => [
        jsonProperty('Songs', jsonDictionary('int32', SongIllustrations.json), { required: true }),
        jsonProperty('UnderGeneration', jsonList(jsonInt32)),
        jsonProperty('Etag', jsonInt32, { required: true }),
    ]);
}
