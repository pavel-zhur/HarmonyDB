import { jsonClass, jsonDictionary, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Chords } from '../Chords';

export class GetSongsResponse {
    readonly songs: Map<string, Chords>;

    constructor(init: { songs: Map<string, Chords> }) {
        this.songs = init.songs;
    }

    static readonly json: JsonContract<GetSongsResponse> = jsonClass<GetSongsResponse>('HarmonyDB.Source.Api.Model.V1.Api.GetSongsResponse', init => new GetSongsResponse(init as never), () => [
        jsonProperty('Songs', jsonDictionary('string', Chords.json), { required: true }),
    ]);
}
