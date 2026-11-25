// What the lead sheet says of a piece, before any measuring or drawing: the bars with their real beats and meters, the
// chords at the places where they start, every sung word at its place in its bar, the sections, the keys and tempi.
// Data in, data out; no DOM.
//
//   musicOf(piece, lyrics, names) → Music
//
//   bars       the piece's bars, each with:
//     pickup     a short first bar (it has the meter's last beats); short: a short last bar (the first ones)
//     free       a bar without a pulse: its beats are moments, a mark each, without lengths
//     signature  { top, bottom } where a time signature is engraved ("2+2+3" over "8"), else null
//     key        the key in force, as the sheet names it; keyChange { from, to } (signatures) where it changes
//     tempo      { bpm, unit, dotted } where a tempo mark stands — the first bar with a pulse, where the number or the
//                note it counts changes, and after a free bar — else null
//     fermata    the bar's held beats, counted from 0
//     section    the name of the section that starts with the bar, else null
//     phrase     how many bars filling their meter the section has had before this one
//     events     the chord symbols: { pos, text, rest } — pos in beats from the bar's start (2.5 half-way through the
//                third beat); rest: no chord ("N.C.")
//     sounding   the symbol still sounding when the bar begins (a reminder at the start of a line), or null
//     notes      what is written on the staff (rhythm.ts)
//     words      { text, t, pos, line }: every word at its place, `line` the lyrics line it belongs to
//   written    whether the chords have their written notes: then they can be written as notes

import type { Lyrics } from '../../../data/analysis';
import type { Piece, Chord } from '../../piece';
import type { Names } from './names';
import { valueOf, WHOLE, writeBar, type Note, type Place, type Span } from './rhythm';
import { parseKey, type Key } from './spelling';
import { writtenHeads, type Sound } from './voicing';

const NO_CHORD = 'N.C.';

export interface ChordEvent { pos: number; text: string; rest: boolean }

export interface Word { text: string; t: number; pos: number; line: number }

export interface Meter {
    /** beats in a whole bar */
    beats: number;
    /** the note the lower number names */
    unit: number;
    /** a beat is a dotted note (6/8, 9/8, 12/8) */
    compound: boolean;
    /** the sizes of the groups a sum names ("2+2+3/8": [2, 2, 3]), each a beat */
    groups: number[] | null;
    /** the upper number of a sum as written */
    text: string | null;
}

export interface MusicBar {
    index: number;
    number: number;
    start: number;
    end: number;
    beats: number[];
    pickup: boolean;
    short: boolean;
    free: boolean;
    signature: { top: string; bottom: string } | null;
    key: Key | null;
    keyChange: { from: number; to: number } | null;
    tempo: { bpm: number; unit: number; dotted: boolean } | null;
    fermata: number[];
    section: string | null;
    phrase: number;
    notes: Note[];
    events: ChordEvent[];
    sounding: ChordEvent | null;
    words: Word[];
    lengths: number[];
    groups: number[];
    triple: boolean[];
}

export interface Music {
    bars: MusicBar[];
    written: boolean;
}

/**
 * The meter of a run of bars under one time signature. A meter of plain numbers is counted by the bars themselves where
 * they disagree with it (the count most of them have).
 */
function parseMeter(text: string, bars: MusicBar[]): Meter {
    const counts = new Map<number, number>();
    for (const b of bars) counts.set(b.beats.length, (counts.get(b.beats.length) ?? 0) + 1);
    let regular = 0;
    for (const [n, c] of counts) if (!regular || c > (counts.get(regular) ?? 0)) regular = n;
    const sum = /^(\d+(?:\+\d+)+)\s*\/\s*(\d+)$/.exec(text.trim());
    if (sum) {
        const groups = sum[1]!.split('+').map(Number);
        return { beats: groups.length, unit: Number(sum[2]), compound: false, groups, text: sum[1]! };
    }
    const m = /^(\d+)\s*\/\s*(\d+)$/.exec(text.trim());
    const unit = m ? Number(m[2]) : 4, written = m ? Number(m[1]) : regular;
    // 6/8, 9/8, 12/8 counted in dotted beats: two, three or four beats to the bar
    const compound = unit === 8 && written > 3 && written % 3 === 0 && regular === written / 3;
    return { beats: regular, unit, compound, groups: null, text: null };
}

/** The time signature of a bar of `count` beats under a meter. */
function signatureFor(count: number, meter: Meter): { top: string; bottom: string } {
    if (meter.groups && meter.text !== null && count === meter.beats) return { top: meter.text, bottom: String(meter.unit) };
    return meter.compound ? { top: String(count * 3), bottom: '8' } : { top: String(count), bottom: String(meter.unit) };
}

/** Each beat's length in sixty-fourths: the meter's beat; of a sum, each group (a pickup has the last groups). */
function beatLengths(count: number, meter: Meter, from: number): number[] {
    const out: number[] = [];
    for (let k = 0; k < count; k++) {
        if (!meter.groups) {
            out.push(meter.compound ? 3 * WHOLE / 8 : WHOLE / meter.unit);
            continue;
        }
        const group = meter.groups[from + k];
        if (group === undefined) throw new Error(`A bar of ${count} beats under a meter of ${meter.groups.length} groups.`);
        out.push(group * WHOLE / meter.unit);
    }
    return out;
}

/**
 * The groups of beats a bar is read in: a bar of up to four is one group, a longer one is fours and what is left (five
 * is three and two), or threes where the meter counts in three. In a sum each group is a beat of its own.
 */
function groupsOf(count: number, meter: Meter): number[] {
    const sizes: number[] = [];
    let left = count;
    if (meter.groups) {
        for (; left > 0; left--) sizes.push(1);
        return sizes;
    }
    if (count > 4 && count % 3 === 0 && meter.beats % 3 === 0) {
        for (; left > 0; left -= 3) sizes.push(3);
        return sizes;
    }
    for (; left > 5; left -= 4) sizes.push(4);
    if (left === 5) sizes.push(3, 2);
    else sizes.push(left);
    return sizes;
}

/** Where time t is in a bar, in beats from its first beat. */
function positionIn(bar: MusicBar, t: number): number {
    const b = bar.beats, n = b.length;
    if (t <= b[0]!) return 0;
    for (let k = n - 1; k >= 0; k--) {
        if (t >= b[k]!) {
            const next = k + 1 < n ? b[k + 1]! : bar.end;
            return next > b[k]! ? Math.min(n, k + (t - b[k]!) / (next - b[k]!)) : k;
        }
    }
    return 0;
}

function barIndexAt(bars: MusicBar[], t: number): number {
    let lo = 0, hi = bars.length - 1;
    while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (bars[mid]!.start <= t) lo = mid;
        else hi = mid - 1;
    }
    return lo;
}

/** The notes a chord is written with: its written notes, at their own octaves; null when it has none. */
function soundOf(chord: Chord): Sound | null {
    if (!chord.writtenNotes?.length) return null;
    const heads = writtenHeads(chord.writtenNotes);
    return heads.length ? { heads } : null;
}

/**
 * The bars' parts say which chords each beat has, each where it begins in its beat ([n, d]: n/d of the beat). What fills
 * each bar from one place to the next becomes its notes. A free bar has a mark at each moment something sounds, without
 * a length.
 */
function placeChords(piece: Piece, bars: MusicBar[], names: Names): void {
    const sounds = new Map<Chord, Sound | null>();
    const soundFor = (c: Chord) => {
        if (!sounds.has(c)) sounds.set(c, soundOf(c));
        return sounds.get(c) ?? null;
    };
    const eventOf = (c: Chord | null, pos: number): ChordEvent => (c?.name ? { pos, text: names.chord(c.name), rest: false } : { pos, text: NO_CHORD, rest: true });
    // what sounds: a chord (a run of one name is one chord here, written with its first chord's notes), or a silence by
    // what is written for it — two silences one after the other are two
    interface Now { chord: Chord | null; silence: string | null }
    const same = (a: Now | null, b: Now | null) => !!a && !!b && a.chord === b.chord && (a.chord !== null || a.silence === b.silence);
    let current: ChordEvent | null = null, previous: Now | null = null;
    for (const bar of bars) {
        const spans: Span[] = [];
        const end: Place = { k: bar.beats.length, n: 0, d: 1 };
        const parts = piece.bars[bar.index]!.parts;
        bar.beats.forEach((_, k) => {
            (parts[k] ?? []).forEach((part, j) => {
                const now: Now = { chord: part.run?.chord ?? null, silence: part.silence };
                if (k === 0 && j === 0) bar.sounding = same(now, previous) ? current : null;
                const pos = k + part.at[0] / part.at[1];
                const sound = now.chord ? soundFor(now.chord) : null;
                if (same(now, previous)) {
                    if (!spans.length) spans.push({ from: { k: 0, n: 0, d: 1 }, to: end, rest: !now.chord, tied: !!now.chord, sound });
                } else {
                    const last = spans[spans.length - 1];
                    if (last) last.to = { k, n: part.at[0], d: part.at[1] };
                    spans.push({ from: { k, n: part.at[0], d: part.at[1] }, to: end, rest: !now.chord, tied: false, sound });
                    // (a stop has no symbol: its rests say it)
                    if (now.chord || now.silence !== 'stop') {
                        current = eventOf(now.chord, pos);
                        bar.events.push(current);
                    } else current = null;
                    previous = now;
                }
            });
        });
        if (bar.free) {
            bar.notes = [];
            for (const span of spans) {
                if (span.rest || span.from.n) continue;
                for (let k = span.from.k; k < (span.to.n ? span.to.k + 1 : span.to.k); k++) {
                    bar.notes.push({ pos: k, beats: 1, unit: 4, dots: 0, tuplet: 0, rest: false, bar: false, tieIn: false, tieOut: false, beam: null, free: true, sound: span.sound });
                }
            }
        } else bar.notes = writeBar(spans, { lengths: bar.lengths, groups: bar.groups, triple: bar.triple });
    }
    let before: Note | null = null;
    for (const bar of bars) {
        for (const note of bar.notes) {
            if (note.tieIn && before) before.tieOut = true;
            before = note;
        }
    }
}

/** Every word at its place; a line without its words' times has its words spread evenly over it. */
function placeWords(bars: MusicBar[], lyrics: Lyrics | null): void {
    (lyrics?.lines ?? []).forEach((line, li) => {
        const words: { text: string; t: number }[] = [];
        if (line.words.length) {
            for (const w of line.words) {
                const text = w.text.trim();
                if (text && w.start !== null && w.start !== undefined) words.push({ text, t: w.start });
            }
        } else if (line.start !== null && line.start !== undefined && line.end !== null && line.end !== undefined) {
            const parts = line.text.trim().split(/\s+/).filter(Boolean);
            const s = line.start, d = (line.end - s) / Math.max(1, parts.length);
            parts.forEach((p, i) => words.push({ text: p, t: s + i * d }));
        }
        for (const w of words) {
            const bar = bars[barIndexAt(bars, w.t)]!;
            bar.words.push({ text: w.text, t: w.t, pos: positionIn(bar, w.t), line: li });
        }
    });
    for (const bar of bars) bar.words.sort((a, b) => a.t - b.t);
}

export function musicOf(piece: Piece, lyrics: Lyrics | null, names: Names): Music {
    const given = piece.bars;
    const keyOf = (key: string | null) => (key ? parseKey(names.key(key)) : null);
    const bars: MusicBar[] = given.map(b => ({
        index: b.index, number: b.number, start: b.start, end: b.end, beats: b.beats,
        pickup: false, short: false, free: b.free, signature: null, key: keyOf(b.key), keyChange: null, tempo: null,
        fermata: b.fermatas.map(f => f - 1), section: null, phrase: 0,
        notes: [], events: [], sounding: null, words: [], lengths: [], groups: [], triple: [],
    }));
    const written = piece.chords.some(c => !!c.writtenNotes?.length);
    if (!bars.length) return { bars, written };

    // each run of bars under one written meter is counted by it; a free bar has none
    const meters: (Meter | null)[] = [];
    for (let i = 0; i < bars.length;) {
        let j = i;
        while (j < bars.length && given[j]!.timeSignature === given[i]!.timeSignature) j++;
        const meter = bars[i]!.free ? null : parseMeter(given[i]!.timeSignature, bars.slice(i, j));
        for (let k = i; k < j; k++) meters[k] = meter;
        i = j;
    }
    // A short first bar is a pickup under its meter, a short last one ends the piece in it: neither says a meter of its
    // own. Every other bar whose count is not its meter's says its own; a time signature stands where what is written
    // changes, and again after a free bar.
    let shown: { top: string; bottom: string } | null = null;
    let lastTempo: { bpm: number; unit: number; dotted: boolean } | null = null;
    bars.forEach((bar, b) => {
        const meter = meters[b], count = bar.beats.length;
        if (!meter) {
            shown = null;
            return;
        }
        bar.pickup = b === 0 && count < meter.beats;
        bar.short = b > 0 && b === bars.length - 1 && given[b]!.lacks?.end === 'end';
        const sig = signatureFor(bar.pickup || bar.short ? meter.beats : count, meter);
        if (!shown || sig.top !== shown.top || sig.bottom !== shown.bottom) bar.signature = sig;
        shown = sig;
        bar.lengths = beatLengths(count, meter, bar.pickup && meter.groups ? meter.beats - count : 0);
        bar.groups = groupsOf(count, meter);
        // (a beat that is three of a note divides in threes without a tuplet: 6/8's, a group of three in a sum)
        bar.triple = bar.lengths.map(len => valueOf(len)?.dots === 1);
        const tempo = given[b]!.tempo;
        if (tempo && tempo.bpm > 0) {
            const mark = { bpm: Math.round(tempo.bpm), unit: tempo.beatUnit.denominator, dotted: !!tempo.beatUnit.dotted };
            if (!lastTempo || lastTempo.bpm !== mark.bpm || lastTempo.unit !== mark.unit || lastTempo.dotted !== mark.dotted || (b > 0 && bars[b - 1]!.free)) bar.tempo = mark;
            lastTempo = mark;
        }
    });
    bars.forEach((bar, b) => {
        if (b === 0) return;
        const from = bars[b - 1]!.key?.signature ?? 0, to = bar.key?.signature ?? 0;
        if (from !== to) bar.keyChange = { from, to };
    });

    placeChords(piece, bars, names);
    placeWords(bars, lyrics);
    for (const section of piece.sections) {
        const bar = bars[section.from], name = section.name.trim();
        if (bar && name) bar.section = bar.section ? `${bar.section} · ${name}` : name;
    }

    // (a phrase counts the bars that fill their meter)
    let phrase = 0;
    bars.forEach((bar, b) => {
        if (bar.section) phrase = 0;
        bar.phrase = phrase;
        const meter = meters[b];
        if (meter && bar.beats.length === meter.beats) phrase++;
    });
    return { bars, written };
}
