// System.DateTime: 100-nanosecond ticks since 0001-01-01 plus a Kind. Comparison and equality use the ticks only,
// ignoring the Kind, as in .NET. The time zone is the browser's, as for .NET running in the browser.

import { bigInt64HashCode } from './hashCode';

export enum DateTimeKind {
    Unspecified = 0,
    Utc = 1,
    Local = 2,
}

const ticksPerMillisecond = 10000n;
const ticksPerMinute = 600000000n;
const unixEpochTicks = 621355968000000000n;
const maxTicks = 3155378975999999999n;

function pad(value: number | bigint, length: number): string {
    return value.toString().padStart(length, '0');
}

/** The local wall-clock fields of a tick count (proleptic Gregorian, as .NET). */
function fieldsOf(ticks: bigint) {
    const ms = Number((ticks - unixEpochTicks) / ticksPerMillisecond - ((ticks - unixEpochTicks) % ticksPerMillisecond < 0n ? 1n : 0n));
    const date = new Date(ms);
    const fraction = ticks % 10000000n;
    return {
        year: date.getUTCFullYear(),
        month: date.getUTCMonth() + 1,
        day: date.getUTCDate(),
        hour: date.getUTCHours(),
        minute: date.getUTCMinutes(),
        second: date.getUTCSeconds(),
        fraction,
    };
}

function ticksOfFields(year: number, month: number, day: number, hour: number, minute: number, second: number): bigint {
    const date = new Date(0);
    date.setUTCFullYear(year, month - 1, day);
    date.setUTCHours(hour, minute, second, 0);
    return BigInt(date.getTime()) * ticksPerMillisecond + unixEpochTicks;
}

/** The local offset at a UTC instant, in ticks (whole minutes, as .NET's offsets). */
function localOffsetOfUtc(utcTicks: bigint): bigint {
    const ms = Number((utcTicks - unixEpochTicks) / ticksPerMillisecond);
    return BigInt(Math.round(-new Date(ms).getTimezoneOffset())) * ticksPerMinute;
}

const ticksPerDay = 864000000000n;

/** The offsets a local wall-clock time can have: one; two in an overlap (a clock set back); none in a gap (a clock set forward). */
function offsetsOfLocal(localTicks: bigint): { valid: bigint[]; standard: bigint } {
    const around = [localOffsetOfUtc(localTicks - ticksPerDay), localOffsetOfUtc(localTicks + ticksPerDay)];
    const candidates = around[0] === around[1] ? [around[0]!] : around;
    const valid = candidates.filter(o => localOffsetOfUtc(localTicks - o) === o);
    const standard = candidates.reduce((a, b) => (a < b ? a : b));
    return { valid, standard };
}

/**
 * TimeZoneInfo.Local.GetUtcOffset for a local wall-clock time, in ticks. A time in a gap or an overlap gets the
 * standard (the smaller) offset, as .NET gives it, except a time .NET marked as the daylight one of an overlap.
 */
function localOffsetOfLocal(localTicks: bigint, ambiguousDst = false): bigint {
    const { valid, standard } = offsetsOfLocal(localTicks);
    if (valid.length === 1) return valid[0]!;
    if (valid.length > 1 && ambiguousDst) return valid.reduce((a, b) => (a > b ? a : b));
    return standard;
}

/** The UTC instant as a local time, clamped to the DateTime range, as DateTime.ToLocalTime does. */
function utcToLocal(utcTicks: bigint): DateTime {
    const offset = localOffsetOfUtc(utcTicks);
    const local = utcTicks + offset;
    if (local > maxTicks) return new DateTime(maxTicks, DateTimeKind.Local);
    if (local < 0n) return new DateTime(0n, DateTimeKind.Local);
    const { valid } = offsetsOfLocal(local);
    return new DateTime(local, DateTimeKind.Local, valid.length > 1 && offset === valid.reduce((a, b) => (a > b ? a : b)));
}

const isoPattern = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,16}))?)?)?(Z|[+-]\d{2}(?::\d{2})?)?$/;

export class DateTime {
    readonly ticks: bigint;
    readonly kind: DateTimeKind;

    /** .NET keeps, in a Local value converted from UTC into an overlap, that it is the daylight one of the two. */
    private readonly ambiguousDst: boolean;

    constructor(ticks: bigint, kind: DateTimeKind = DateTimeKind.Unspecified, ambiguousDst = false) {
        if (ticks < 0n || ticks > maxTicks) throw new RangeError('Ticks must be between DateTime.MinValue.Ticks and DateTime.MaxValue.Ticks.');
        this.ticks = ticks;
        this.kind = kind;
        this.ambiguousDst = kind === DateTimeKind.Local && ambiguousDst;
    }

    static readonly minValue = new DateTime(0n);

    /** DateTime.Now */
    static now(): DateTime {
        return utcToLocal(BigInt(Date.now()) * ticksPerMillisecond + unixEpochTicks);
    }

    /** DateTime.UtcNow */
    static utcNow(): DateTime {
        return new DateTime(BigInt(Date.now()) * ticksPerMillisecond + unixEpochTicks, DateTimeKind.Utc);
    }

    compareTo(other: DateTime): number {
        return this.ticks < other.ticks ? -1 : this.ticks > other.ticks ? 1 : 0;
    }

    equals(other: DateTime | null | undefined): boolean {
        return other != null && this.ticks === other.ticks;
    }

    getHashCode(): number {
        return bigInt64HashCode(this.ticks);
    }

    /** DateTime.ToLocalTime: a Local value stays; Utc and Unspecified are taken as UTC. Clamped to the DateTime range. */
    toLocalTime(): DateTime {
        if (this.kind === DateTimeKind.Local) return this;
        return utcToLocal(this.ticks);
    }

    /** DateTime.ToUniversalTime: a Utc value stays; Local and Unspecified are taken as local. Clamped to the DateTime range. */
    toUniversalTime(): DateTime {
        if (this.kind === DateTimeKind.Utc) return this;
        const utc = this.ticks - localOffsetOfLocal(this.ticks, this.ambiguousDst);
        return new DateTime(utc < 0n ? 0n : utc > maxTicks ? maxTicks : utc, DateTimeKind.Utc);
    }

    /** The wall-clock fields of this value (whatever its Kind). */
    get year(): number { return fieldsOf(this.ticks).year; }
    get month(): number { return fieldsOf(this.ticks).month; }
    get day(): number { return fieldsOf(this.ticks).day; }
    get hour(): number { return fieldsOf(this.ticks).hour; }
    get minute(): number { return fieldsOf(this.ticks).minute; }
    get second(): number { return fieldsOf(this.ticks).second; }

    /** How System.Text.Json reads a DateTime: ISO 8601; "Z" gives Utc, an offset gives Local (converted), none gives Unspecified. */
    static parseJson(value: string): DateTime | null {
        const m = isoPattern.exec(value);
        if (!m) return null;
        const [, y, mo, d, h, mi, s, fr, offset] = m;
        const year = Number(y), month = Number(mo), day = Number(d);
        const hour = h ? Number(h) : 0, minute = mi ? Number(mi) : 0, second = s ? Number(s) : 0;
        if (month < 1 || month > 12 || day < 1 || hour > 23 || minute > 59 || second > 59 || year < 1) return null;
        const probe = new Date(0);
        probe.setUTCFullYear(year, month - 1, day);
        if (probe.getUTCDate() !== day) return null;
        const fraction = fr ? BigInt((fr + '000000').substring(0, 7)) : 0n;
        const wall = ticksOfFields(year, month, day, hour, minute, second) + fraction;
        if (offset === undefined) return new DateTime(wall, DateTimeKind.Unspecified);
        if (offset === 'Z') return new DateTime(wall, DateTimeKind.Utc);
        const sign = offset[0] === '-' ? -1n : 1n;
        const offsetHours = BigInt(offset.substring(1, 3));
        const offsetMinutes = offset.length > 3 ? BigInt(offset.substring(4, 6)) : 0n;
        if (offsetHours > 14n || offsetMinutes > 59n) return null;
        const utc = wall - sign * (offsetHours * 60n + offsetMinutes) * ticksPerMinute;
        // Out of the DateTime range as a UTC time: System.Text.Json does not read it.
        if (utc < 0n || utc > maxTicks) return null;
        return utcToLocal(utc);
    }

    /** How System.Text.Json writes a DateTime: the round-trip format with the fraction's trailing zeros trimmed. */
    toJson(): string {
        const f = fieldsOf(this.ticks);
        let result = `${pad(f.year, 4)}-${pad(f.month, 2)}-${pad(f.day, 2)}T${pad(f.hour, 2)}:${pad(f.minute, 2)}:${pad(f.second, 2)}`;
        if (f.fraction !== 0n) result += '.' + pad(f.fraction, 7).replace(/0+$/, '');
        if (this.kind === DateTimeKind.Utc) {
            result += 'Z';
        } else if (this.kind === DateTimeKind.Local) {
            const offsetMinutes = Number(localOffsetOfLocal(this.ticks, this.ambiguousDst) / ticksPerMinute);
            const abs = Math.abs(offsetMinutes);
            result += `${offsetMinutes < 0 ? '-' : '+'}${pad(Math.floor(abs / 60), 2)}:${pad(abs % 60, 2)}`;
        }
        return result;
    }
}
