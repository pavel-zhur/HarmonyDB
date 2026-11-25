// What the roll shows: the notes part by part, and the drums, in arrays that are quick to search. No DOM.
//
//   const data = readNotes(notes)
//   data.lanes       a lane a part, in the parts' order: its notes ordered by start (start, end: seconds; pitch;
//                     velocity, NONE where a note has none, or null where none has one); `joined[i]` is 1 when note
//                     i+1 follows note i closely enough to read as one line (a part that plays one note at a time)
//   data.drums       null, or the drum lane: its hits ordered by start, each in the row of its kind of drum; `rows`:
//                     the kinds that occur, in their order (drums.ts)
//   data.velocity    whether any note or hit has a velocity
//   data.end         where the last note ends, or the last hit is, seconds

import type { Notes } from '../../../data/analysis';
import { DRUM_KINDS, drumKind, type DrumKindId } from './drums';

export type Stem = NonNullable<Notes['parts'][number]['stem']>;

// Two notes of a one-voice part read as one line when the second starts within this long of the first one's end, no
// further than an octave away. A longer gap is a rest; a wider leap a new phrase: the line breaks there.
const JOIN_SECONDS = 0.4, JOIN_SEMITONES = 12;

/** How many colours the parts have (roll.css, LANE_TOKENS); a part past them takes them again. */
export const LANE_COLOURS = 5;
/** The velocity of a note that has none. */
export const NONE = 255;
const EPS = 1e-6;

/** What can be searched by start. */
export interface Timed {
    count: number;
    start: Float64Array;
}

export interface Lane extends Timed {
    id: string;
    name: string;
    stem: Stem | null;
    /** its General MIDI program, when the analysis says */
    program: number | null;
    /** its colour, of LANE_COLOURS */
    colour: number;
    end: Float64Array;
    pitch: Uint8Array;
    velocity: Uint8Array | null;
    longest: number;
    low: number;
    high: number;
    oneVoice: boolean;
    joined: Uint8Array;
    last: number;
}

export interface DrumLane extends Timed {
    id: 'drums';
    drum: Uint8Array;
    velocity: Uint8Array | null;
    row: Uint8Array;
    rows: { id: DrumKindId; count: number }[];
    last: number;
}

export interface RollNotes {
    lanes: Lane[];
    drums: DrumLane | null;
    velocity: boolean;
    end: number;
}

function laneOf(index: number, part: Notes['parts'][number]): Lane {
    const notes = part.notes.slice().sort((a, b) => a.start - b.start || a.pitch - b.pitch);
    const n = notes.length;
    const start = new Float64Array(n), end = new Float64Array(n), pitch = new Uint8Array(n), velocity = new Uint8Array(n);
    let longest = 0, low = 127, high = 0, oneVoice = n > 1, latest = -Infinity, loud = false;
    notes.forEach((note, i) => {
        start[i] = note.start;
        end[i] = note.end;
        pitch[i] = note.pitch;
        velocity[i] = note.velocity ?? NONE;
        loud ||= note.velocity != null;
        longest = Math.max(longest, note.end - note.start);
        low = Math.min(low, note.pitch);
        high = Math.max(high, note.pitch);
        if (note.start < latest - EPS) oneVoice = false;
        latest = Math.max(latest, note.end);
    });
    const joined = new Uint8Array(n);
    if (oneVoice) {
        for (let i = 0; i + 1 < n; i++) {
            joined[i] = start[i + 1]! - end[i]! <= JOIN_SECONDS && Math.abs(pitch[i + 1]! - pitch[i]!) <= JOIN_SEMITONES ? 1 : 0;
        }
    }
    return {
        id: `p${index}`, name: part.name, stem: part.stem ?? null, program: part.program ?? null, colour: index % LANE_COLOURS,
        count: n, start, end, pitch, velocity: loud ? velocity : null, longest, low, high, oneVoice, joined, last: n ? latest : 0,
    };
}

function drumsOf(list: Notes['drums']): DrumLane {
    const hits = list.slice().sort((a, b) => a.start - b.start || a.drum - b.drum);
    const n = hits.length;
    const start = new Float64Array(n), drum = new Uint8Array(n), velocity = new Uint8Array(n), kind = new Uint8Array(n);
    const counts = new Array<number>(DRUM_KINDS.length).fill(0);
    let loud = false;
    hits.forEach((hit, i) => {
        start[i] = hit.start;
        drum[i] = hit.drum;
        velocity[i] = hit.velocity ?? NONE;
        loud ||= hit.velocity != null;
        const k = drumKind(hit.drum);
        kind[i] = k;
        counts[k]!++;
    });
    // a row for each kind that occurs, in the kinds' order
    const rowOf = new Int16Array(DRUM_KINDS.length).fill(-1), rows: DrumLane['rows'] = [];
    DRUM_KINDS.forEach((k, i) => {
        if (!counts[i]) return;
        rowOf[i] = rows.length;
        rows.push({ id: k.id, count: counts[i]! });
    });
    const row = new Uint8Array(n);
    for (let i = 0; i < n; i++) row[i] = rowOf[kind[i]!]!;
    return { id: 'drums', count: n, start, drum, velocity: loud ? velocity : null, row, rows, last: n ? start[n - 1]! : 0 };
}

export function readNotes(notes: Notes): RollNotes {
    const lanes = notes.parts.map((part, i) => laneOf(i, part));
    const drums = notes.drums.length ? drumsOf(notes.drums) : null;
    return {
        lanes, drums,
        velocity: lanes.some(lane => lane.velocity) || !!drums?.velocity,
        end: Math.max(0, ...lanes.map(lane => lane.last), drums ? drums.last : 0),
    };
}

/** Index of the first item with start >= t. */
function lowerBound(starts: Float64Array, t: number): number {
    let lo = 0, hi = starts.length;
    while (lo < hi) {
        const mid = (lo + hi) >> 1;
        if (starts[mid]! < t) lo = mid + 1;
        else hi = mid;
    }
    return lo;
}

/** Index of the first note that can still sound at t or later. */
export function firstFrom(lane: Lane, t: number): number {
    return lowerBound(lane.start, t - lane.longest);
}

/** [first, past): the notes (or hits) that start in a <= start < b. */
export function startingIn(lane: Timed, a: number, b: number): [number, number] {
    return [lowerBound(lane.start, a), lowerBound(lane.start, b)];
}

/** The notes that sound at t (start <= t < end). */
export function soundingAt(lane: Lane, t: number): number[] {
    const found: number[] = [];
    for (let i = firstFrom(lane, t); i < lane.count && lane.start[i]! <= t; i++) if (lane.end[i]! > t) found.push(i);
    return found;
}

/** The note of that pitch sounding at t, or −1. */
export function noteAt(lane: Lane, t: number, pitch: number): number {
    for (let i = firstFrom(lane, t); i < lane.count && lane.start[i]! <= t; i++) if (lane.end[i]! > t && lane.pitch[i] === pitch) return i;
    return -1;
}

/** The lowest and the highest pitch of those lanes, or null when they have no notes. */
export function pitchRange(lanes: readonly Lane[]): { low: number; high: number } | null {
    let low = Infinity, high = -Infinity;
    for (const lane of lanes) {
        if (!lane.count) continue;
        low = Math.min(low, lane.low);
        high = Math.max(high, lane.high);
    }
    return low <= high ? { low, high } : null;
}
