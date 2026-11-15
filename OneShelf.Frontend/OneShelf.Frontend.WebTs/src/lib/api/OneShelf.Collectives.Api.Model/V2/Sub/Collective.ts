import { jsonClass, jsonInt32, jsonList, jsonNullable, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { CollectiveVisibility, CollectiveVisibilityJson } from './CollectiveVisibility';

export interface CollectiveInit {
    authors: string[];
    title: string;
    contents: string;
    visibility?: CollectiveVisibility;
    description?: string | null;
    tonality?: string | null;
    bpm?: number | null;
    versionComment?: string | null;
}

export class Collective {
    authors: string[];
    title: string;
    contents: string;
    visibility: CollectiveVisibility;
    description: string | null;
    tonality: string | null;
    bpm: number | null;
    versionComment: string | null;

    constructor(init: CollectiveInit) {
        this.authors = init.authors;
        this.title = init.title;
        this.contents = init.contents;
        this.visibility = init.visibility ?? CollectiveVisibility.Private;
        this.description = init.description ?? null;
        this.tonality = init.tonality ?? null;
        this.bpm = init.bpm ?? null;
        this.versionComment = init.versionComment ?? null;
    }

    static readonly json: JsonContract<Collective> = jsonClass<Collective>('OneShelf.Collectives.Api.Model.V2.Sub.Collective', init => new Collective(init as unknown as CollectiveInit), () => [
        jsonProperty('Authors', jsonList(jsonString), { required: true }),
        jsonProperty('Title', jsonString, { required: true }),
        jsonProperty('Contents', jsonString, { required: true }),
        jsonProperty('Visibility', CollectiveVisibilityJson),
        jsonProperty('Description', jsonString),
        jsonProperty('Tonality', jsonString),
        jsonProperty('Bpm', jsonNullable(jsonInt32)),
        jsonProperty('VersionComment', jsonString),
    ]);
}
