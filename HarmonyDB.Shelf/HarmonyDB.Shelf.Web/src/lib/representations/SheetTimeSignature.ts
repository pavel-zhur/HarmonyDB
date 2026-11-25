import { ArgumentOutOfRangeException } from '../common/dotnet/exceptions';
import { jsonClass, jsonInt32, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';

/** A time signature: so many beats in a bar (`beats`), each the note `unit` (4 a quarter, 8 an eighth…): 3/4, 6/8. A record. */
export class SheetTimeSignature {
    readonly beats: number;
    readonly unit: number;

    constructor(beats: number, unit: number) {
        if (beats <= 0)
            throw new ArgumentOutOfRangeException('beats');

        if (![1, 2, 4, 8, 16, 32].includes(unit))
            throw new ArgumentOutOfRangeException('unit');

        this.beats = beats;
        this.unit = unit;
    }

    /** Reads "3/4". */
    static parse(text: string): SheetTimeSignature {
        const parts = text.split('/');
        if (parts.length === 2 && /^[+-]?\d+$/.test(parts[0]!.trim()) && /^[+-]?\d+$/.test(parts[1]!.trim()))
            return new SheetTimeSignature(parseInt(parts[0]!, 10), parseInt(parts[1]!, 10));

        throw new ArgumentOutOfRangeException('text');
    }

    toString(): string {
        return `${this.beats}/${this.unit}`;
    }

    static readonly json: JsonContract<SheetTimeSignature> = jsonClass<SheetTimeSignature>('HarmonyDB.Common.Representations.OneShelf.SheetTimeSignature',
        init => new SheetTimeSignature(init.beats as number, init.unit as number), () => [
            jsonProperty('Beats', jsonInt32),
            jsonProperty('Unit', jsonInt32),
        ]);
}
