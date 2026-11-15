// The original: Models/IllustrationsCache.cs — what IllustrationsProvider keeps in localStorage ("IllustrationsProviderV3").

import { AllIllustrations } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Illustrations/AllIllustrations';
import type { DateTime } from '../lib/common/dotnet/DateTime';
import { jsonClass, jsonDateTime, jsonProperty, type JsonContract } from '../lib/common/dotnet/json/JsonSerializer';

export class IllustrationsCache {
    readonly all: AllIllustrations;
    readonly receivedOn: DateTime;

    constructor(init: { all: AllIllustrations; receivedOn: DateTime }) {
        this.all = init.all;
        this.receivedOn = init.receivedOn;
    }

    static readonly json: JsonContract<IllustrationsCache> = jsonClass<IllustrationsCache>('OneShelf.Frontend.Web.Models.IllustrationsCache', init => new IllustrationsCache(init as never), () => [
        jsonProperty('All', AllIllustrations.json, { required: true }),
        jsonProperty('ReceivedOn', jsonDateTime, { required: true }),
    ]);
}