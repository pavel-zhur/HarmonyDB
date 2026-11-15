// DateTime.ToLongDateString() and ToShortTimeString() under the invariant culture (the site runs with
// InvariantGlobalization): "dddd, dd MMMM yyyy" and "HH:mm".

import type { DateTime } from './DateTime';

const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

function pad2(value: number): string {
    return value.toString().padStart(2, '0');
}

/** DateTime.ToLongDateString() */
export function toLongDateString(value: DateTime): string {
    const date = new Date(0);
    date.setUTCFullYear(value.year, value.month - 1, value.day);
    return `${dayNames[date.getUTCDay()]}, ${pad2(value.day)} ${monthNames[value.month - 1]} ${value.year.toString().padStart(4, '0')}`;
}

/** DateTime.ToShortTimeString() */
export function toShortTimeString(value: DateTime): string {
    return `${pad2(value.hour)}:${pad2(value.minute)}`;
}
