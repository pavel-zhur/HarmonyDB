import { jsonClass, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';

export class FormatPreviewRequest {
    readonly identity: Identity;
    readonly content: string;

    constructor(init: { identity: Identity; content: string }) {
        this.identity = init.identity;
        this.content = init.content;
    }

    static readonly json: JsonContract<FormatPreviewRequest> = jsonClass<FormatPreviewRequest>('OneShelf.Frontend.Api.Model.V3.Api.FormatPreviewRequest', init => new FormatPreviewRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('Content', jsonString, { required: true }),
    ]);
}
