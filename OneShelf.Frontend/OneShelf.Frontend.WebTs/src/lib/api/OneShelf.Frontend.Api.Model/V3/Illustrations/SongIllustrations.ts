import type { DateTime } from '../../../../common/dotnet/DateTime';
import { jsonClass, jsonDateTime, jsonList, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { SongIllustration } from './SongIllustration';

export class SongIllustrations {
    readonly illustrations: readonly SongIllustration[];
    readonly earliestCreatedOn: DateTime;
    readonly latestCreatedOn: DateTime;

    constructor(init: { illustrations: readonly SongIllustration[]; earliestCreatedOn: DateTime; latestCreatedOn: DateTime }) {
        this.illustrations = init.illustrations;
        this.earliestCreatedOn = init.earliestCreatedOn;
        this.latestCreatedOn = init.latestCreatedOn;
    }

    static readonly json: JsonContract<SongIllustrations> = jsonClass<SongIllustrations>('OneShelf.Frontend.Api.Model.V3.Illustrations.SongIllustrations', init => new SongIllustrations(init as never), () => [
        jsonProperty('Illustrations', jsonList(SongIllustration.json), { required: true }),
        jsonProperty('EarliestCreatedOn', jsonDateTime, { required: true }),
        jsonProperty('LatestCreatedOn', jsonDateTime, { required: true }),
    ]);
}
