import { jsonClass, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { NodeHtml } from '../../../../representations/NodeHtml';

export class FormatPreviewResponse {
    readonly output: NodeHtml;

    constructor(init: { output: NodeHtml }) {
        this.output = init.output;
    }

    static readonly json: JsonContract<FormatPreviewResponse> = jsonClass<FormatPreviewResponse>('OneShelf.Frontend.Api.Model.V3.Api.FormatPreviewResponse', init => new FormatPreviewResponse(init as never), () => [
        jsonProperty('Output', () => NodeHtml.json, { required: true }),
    ]);
}
