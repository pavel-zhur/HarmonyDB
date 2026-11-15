import { jsonBoolean, jsonClass, jsonProperty, jsonString, type JsonContract, type JsonPropertyDefinition } from '../../../common/dotnet/json/JsonSerializer';
import { NodeHtml } from '../../../representations/NodeHtml';
import { PublicHeaderBase, type PublicHeaderBaseInit } from './PublicHeaderBase';

export interface ChordsInit extends PublicHeaderBaseInit {
    output: NodeHtml;
    bestTonality: string | null;
    isStable: boolean;
    isPublic: boolean;
    unstableErrorMessage: string | null;
}

/** A record. */
export class Chords extends PublicHeaderBase {
    readonly output: NodeHtml;
    bestTonality: string | null;
    readonly isStable: boolean;
    readonly isPublic: boolean;
    readonly unstableErrorMessage: string | null;

    constructor(init: ChordsInit) {
        super(init);
        this.output = init.output;
        this.bestTonality = init.bestTonality;
        this.isStable = init.isStable;
        this.isPublic = init.isPublic;
        this.unstableErrorMessage = init.unstableErrorMessage;
    }

    static override readonly jsonProperties = (): JsonPropertyDefinition[] => [
        jsonProperty('Output', () => NodeHtml.json, { required: true }),
        jsonProperty('BestTonality', jsonString, { required: true }),
        jsonProperty('IsStable', jsonBoolean, { required: true }),
        jsonProperty('IsPublic', jsonBoolean, { required: true }),
        jsonProperty('UnstableErrorMessage', jsonString, { required: true }),
        ...PublicHeaderBase.jsonProperties(),
    ];

    static readonly json: JsonContract<Chords> = jsonClass<Chords>('HarmonyDB.Source.Api.Model.V1.Chords', init => new Chords(init as unknown as ChordsInit), Chords.jsonProperties);
}
