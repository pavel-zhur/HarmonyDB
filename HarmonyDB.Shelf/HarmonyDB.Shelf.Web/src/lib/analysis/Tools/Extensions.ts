import { ArgumentOutOfRangeException } from '../../common/dotnet/exceptions';
import { compareInts } from '../../common/dotnet/Numbers';
import { first, orderBy } from '../../common/dotnet/Linq';
import type { Out } from '../../common/dotnet/Out';
import { withIndices, withIsFirst } from '../../common/OneShelf.Common/Extensions';
import { ChordType } from '../Models/ChordType';
import { ChordTypePresentation } from '../Models/ChordTypePresentation';

export function chordTypeToString(chordType: ChordType, presentation: ChordTypePresentation = ChordTypePresentation.Default): string {
    switch (chordType) {
        case ChordType.Major:
            return presentation === ChordTypePresentation.MajorAsM ? 'M' : '';
        case ChordType.Minor:
            return presentation !== ChordTypePresentation.Degrees ? 'm' : '';
        case ChordType.Power:
            return '5';
        case ChordType.Sus2:
            return 'sus2';
        case ChordType.Sus4:
            return 'sus4';
        case ChordType.Diminished:
            return 'dim';
        case ChordType.Augmented:
            return 'aug';
        case ChordType.Unknown:
            return '?';
        default:
            throw new ArgumentOutOfRangeException(undefined, `Unexpected chord type. (Parameter 'chordType')\nActual value was ${chordType}.`);
    }
}

export function shiftLoop<T>(source: Iterable<T>, shiftLoop: boolean, wantsToGoFirst: (item: T) => number, shift: Out<number | null>): T[] {
    shift.value = null;
    if (!shiftLoop)
        return [...source];

    const list = [...source];
    const shiftValue = first(orderBy(withIndices(list), (a, b) => compareInts(wantsToGoFirst(b.x), wantsToGoFirst(a.x)) || compareInts(a.i, b.i))).i;
    shift.value = shiftValue;
    return [...list, ...list].slice(shiftValue).slice(0, list.length);
}

/** For an empty source and loopify, the C# yields default(T), which has no generic counterpart here; it yields undefined. */
export function loopify<T>(source: Iterable<T>, loopify: boolean): T[] {
    const result: T[] = [];
    let firstItem: T | undefined = undefined;
    for (const { x: item, isFirst } of withIsFirst(source)) {
        if (isFirst) {
            firstItem = item;
        }

        result.push(item);
    }

    if (loopify) {
        result.push(firstItem as T);
    }

    return result;
}

export function isBetween(value: number, start: number, end: number): boolean {
    return value >= start && value <= end;
}
