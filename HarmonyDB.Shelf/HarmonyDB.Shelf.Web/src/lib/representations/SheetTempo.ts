import { ArgumentOutOfRangeException } from '../common/dotnet/exceptions';
import { jsonBoolean, jsonClass, jsonDouble, jsonInt32, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';

/** A tempo: so many beats a minute, a beat being the note `unit` (4 a quarter, 8 an eighth…), dotted or not. A record. */
export class SheetTempo {
    readonly beatsPerMinute: number;
    readonly unit: number;
    readonly dotted: boolean;

    constructor(beatsPerMinute: number, unit = 4, dotted = false) {
        if (beatsPerMinute <= 0)
            throw new ArgumentOutOfRangeException('beatsPerMinute');

        if (![1, 2, 4, 8, 16].includes(unit))
            throw new ArgumentOutOfRangeException('unit');

        this.beatsPerMinute = beatsPerMinute;
        this.unit = unit;
        this.dotted = dotted;
    }

    static readonly json: JsonContract<SheetTempo> = jsonClass<SheetTempo>('HarmonyDB.Common.Representations.OneShelf.SheetTempo',
        init => new SheetTempo(init.beatsPerMinute as number, (init.unit ?? 4) as number, (init.dotted ?? false) as boolean), () => [
            jsonProperty('BeatsPerMinute', jsonDouble),
            jsonProperty('Unit', jsonInt32),
            jsonProperty('Dotted', jsonBoolean),
        ]);
}
