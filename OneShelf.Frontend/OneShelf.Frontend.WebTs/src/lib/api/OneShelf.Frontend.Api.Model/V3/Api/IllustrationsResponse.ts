import { jsonClass, jsonDictionary, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { AllIllustrations } from '../Illustrations/AllIllustrations';

export class IllustrationsResponse {
    readonly illustrations: AllIllustrations | null;
    readonly titles: Map<number, string> | null;

    constructor(init: { illustrations: AllIllustrations | null; titles: Map<number, string> | null }) {
        this.illustrations = init.illustrations;
        this.titles = init.titles;
    }

    static readonly json: JsonContract<IllustrationsResponse> = jsonClass<IllustrationsResponse>('OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse', init => new IllustrationsResponse(init as never), () => [
        jsonProperty('Illustrations', AllIllustrations.json, { required: true }),
        jsonProperty('Titles', jsonDictionary('int32', jsonString), { required: true }),
    ]);
}
