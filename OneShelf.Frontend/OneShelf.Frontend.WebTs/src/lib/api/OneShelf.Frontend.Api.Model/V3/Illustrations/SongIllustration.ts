import { jsonClass, jsonInt32, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { ImagePublicUrls } from './ImagePublicUrls';

export interface SongIllustrationInit {
    version: number;
    i: number;
    j: number;
    k: number;
    publicUrls: ImagePublicUrls;
    alterationTitle?: string | null;
}

export class SongIllustration {
    version: number;
    i: number;
    j: number;
    k: number;
    readonly publicUrls: ImagePublicUrls;
    alterationTitle: string | null;

    constructor(init: SongIllustrationInit) {
        this.version = init.version;
        this.i = init.i;
        this.j = init.j;
        this.k = init.k;
        this.publicUrls = init.publicUrls;
        this.alterationTitle = init.alterationTitle ?? null;
    }

    static readonly json: JsonContract<SongIllustration> = jsonClass<SongIllustration>('OneShelf.Frontend.Api.Model.V3.Illustrations.SongIllustration', init => new SongIllustration(init as unknown as SongIllustrationInit), () => [
        jsonProperty('Version', jsonInt32, { required: true }),
        jsonProperty('I', jsonInt32, { required: true }),
        jsonProperty('J', jsonInt32, { required: true }),
        jsonProperty('K', jsonInt32, { required: true }),
        jsonProperty('PublicUrls', ImagePublicUrls.json, { required: true }),
        jsonProperty('AlterationTitle', jsonString),
    ]);
}
