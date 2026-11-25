import { jsonClass, jsonDouble, jsonInt32, jsonList, jsonProperty, jsonString, type JsonContract } from '../common/dotnet/json/JsonSerializer';

/** int.TryParse: an integer of int's range, signs and the white space around it allowed. */
function tryParseInt32(text: string): number | null {
    if (!/^\s*[+-]?\d+\s*$/.test(text)) return null;
    const value = Number(text.trim());
    return value >= -2147483648 && value <= 2147483647 ? value : null;
}

/**
 * A musical place in the sheet's bars (SheetTimes): a bar and a beat, counted from 1, and how far into the beat
 * as a fraction below 1 ("1/2", "3/4"; "2/4" is "1/2"), left out on the beat. A record.
 */
export class SheetPlace {
    readonly bar: number;
    readonly beat: number;
    /** [JsonIgnore(WhenWritingNull)] */
    readonly at: string | null;
    /** internal, [JsonIgnore]: `at` read: 0 on the beat; none (null) when it is not a fraction from 0 up to 1, 1 not included. */
    readonly fraction: { numerator: number; denominator: number } | null;

    /**
     * Keeps what is given as it is, also a place that cannot be resolved (a bar or a beat below 1, an `at` that is not a
     * fraction below 1): such a place gives its node no time (SheetTimes.moment), and the sheet is read and written back unchanged.
     */
    constructor(bar: number, beat: number, at: string | null = null) {
        this.bar = bar;
        this.beat = beat;
        this.at = at;
        this.fraction = at === null ? { numerator: 0, denominator: 1 } : SheetPlace.parseFraction(at);
    }

    private static parseFraction(at: string): { numerator: number; denominator: number } | null {
        const parts = at.split('/');
        if (parts.length !== 2) return null;
        const numerator = tryParseInt32(parts[0]!), denominator = tryParseInt32(parts[1]!);
        return numerator !== null && denominator !== null && numerator >= 0 && denominator > 0 && numerator < denominator
            ? { numerator, denominator }
            : null;
    }

    equals(other: SheetPlace | null | undefined): boolean {
        return other != null && this.bar === other.bar && this.beat === other.beat && this.at === other.at;
    }

    static readonly json: JsonContract<SheetPlace> = jsonClass<SheetPlace>('HarmonyDB.Common.Representations.OneShelf.SheetPlace',
        init => new SheetPlace((init.bar ?? 0) as number, (init.beat ?? 0) as number, (init.at ?? null) as string | null), () => [
            jsonProperty('Bar', jsonInt32),
            jsonProperty('Beat', jsonInt32),
            jsonProperty('At', jsonString, { ignoreWhenWritingNull: true }),
        ]);
}

/** A bar: its beats in seconds, the first where the bar begins; its time signature where it is said or changes, held until the next. A record. */
export class SheetBar {
    readonly beats: readonly number[];
    /**
     * "4/4", "12/8", "2+2+3/8" (an additive one), "free" (a bar of no metre). Named in snake case in JSON whatever the naming of the rest:
     * the bars are a structure shared with other formats as it is. [JsonPropertyName("time_sig")], [JsonIgnore(WhenWritingNull)]
     */
    readonly timeSignature: string | null;

    constructor(init: { beats: readonly number[]; timeSignature?: string | null }) {
        this.beats = init.beats;
        this.timeSignature = init.timeSignature ?? null;
    }

    static readonly json: JsonContract<SheetBar> = jsonClass<SheetBar>('HarmonyDB.Common.Representations.OneShelf.SheetBar',
        init => new SheetBar({ beats: init.beats as number[], timeSignature: init.timeSignature as string | null | undefined }), () => [
            jsonProperty('Beats', () => jsonList(jsonDouble), { required: true }),
            jsonProperty('TimeSignature', jsonString, { jsonName: 'time_sig', ignoreWhenWritingNull: true }),
        ]);
}

/**
 * The sheet's own bars, when it has them: the chords and the marks may then stand at places in them (SheetPlace)
 * instead of at seconds. A record.
 */
export class SheetTimes {
    /** Where the last bar ends, in seconds. */
    readonly end: number;
    readonly bars: readonly SheetBar[];

    constructor(init: { end: number; bars: readonly SheetBar[] }) {
        this.end = init.end;
        this.bars = init.bars;
    }

    /**
     * The moment of a place, in seconds: the time of its beat, and for a place inside the beat (SheetPlace.at)
     * that part of the way to the next beat — the bar's next beat, else the next bar's first, else `end`:
     * t(beat) + at × (t(next beat) − t(beat)).
     * None (null) when the place cannot be resolved: the bar or the beat is below 1 or not in the bars, the fraction is not one from 0 up to 1
     * (1 not included), or the next beat is not there to measure to.
     */
    moment(place: SheetPlace): number | null {
        const fraction = place.fraction;
        if (fraction === null) return null;
        const { numerator, denominator } = fraction;
        if (place.bar < 1 || place.bar > this.bars.length) return null;
        const beats = this.bars[place.bar - 1]!.beats;
        if (place.beat < 1 || place.beat > beats.length) return null;

        const time = beats[place.beat - 1]!;
        if (numerator === 0) return time;

        let next: number;
        if (place.beat < beats.length) next = beats[place.beat]!;
        else if (place.bar < this.bars.length && this.bars[place.bar]!.beats.length > 0) next = this.bars[place.bar]!.beats[0]!;
        else if (place.bar === this.bars.length) next = this.end;
        else return null;

        return time + numerator * (next - time) / denominator;
    }

    /**
     * The time signature in force in the bar (counted from 1): the one written on it or on the last bar before it that has one;
     * none (null) when no bar up to it has one or the bar is not in the bars. "4/4", "2+2+3/8", "free".
     */
    timeSignatureOf(bar: number): string | null {
        if (bar < 1 || bar > this.bars.length) return null;
        for (let i = bar - 1; i >= 0; i--)
            if (this.bars[i]!.timeSignature !== null)
                return this.bars[i]!.timeSignature;

        return null;
    }

    static readonly json: JsonContract<SheetTimes> = jsonClass<SheetTimes>('HarmonyDB.Common.Representations.OneShelf.SheetTimes',
        init => new SheetTimes({ end: init.end as number, bars: init.bars as SheetBar[] }), () => [
            jsonProperty('End', jsonDouble, { required: true }),
            jsonProperty('Bars', () => jsonList(SheetBar.json), { required: true }),
        ]);
}
