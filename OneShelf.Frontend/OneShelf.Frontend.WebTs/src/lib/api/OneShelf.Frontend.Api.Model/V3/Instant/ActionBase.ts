import type { DateTime } from '../../../../common/dotnet/DateTime';
import { newGuid, type Guid } from '../../../../common/dotnet/Guid';
import { jsonDateTime, jsonGuid, jsonProperty, type JsonPropertyDefinition } from '../../../../common/dotnet/json/JsonSerializer';

export interface ActionBaseInit {
    actionId?: Guid;
    happenedOn: DateTime;
}

/** A record. ActionId defaults to a new Guid (also when the JSON has none). */
export class ActionBase {
    actionId: Guid;
    happenedOn: DateTime;

    constructor(init: ActionBaseInit) {
        this.actionId = init.actionId !== undefined ? init.actionId : newGuid();
        this.happenedOn = init.happenedOn;
    }

    static readonly jsonProperties = (): JsonPropertyDefinition[] => [
        jsonProperty('ActionId', jsonGuid),
        jsonProperty('HappenedOn', jsonDateTime, { required: true }),
    ];
}
