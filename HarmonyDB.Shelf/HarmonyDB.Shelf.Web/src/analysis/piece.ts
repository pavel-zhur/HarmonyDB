// An analysis as a piece of music: what its views read of its time line — the bars with what is in force in each (meter,
// tempo, key, held beats, a short first or last bar), which chords stand on each beat, the chords in time, the
// sections on the bar lines, and where the key, the meter or the tempo changes.
//
//   pieceOf(timeline) → Piece
//
// Which chords a beat has: where every chord is placed in its bar (a beat and a fraction of it), by those places; else
// by the chords' times, every change on the beat nearest to it (chordsOnBeats). Neighbours of one name are one "run" (a
// chord held over a bar line is not a new one); a chord the analysis writes again where it is struck again begins a run
// of its own.

import type { Timeline, TimelineBar, TimelineChord, TimelineSection } from '../data/analysis';

export interface Chord {
    index: number;
    start: number;
    end: number;
    /** the name with its bass ("Am7/G"); null where no chord is written (N.C.) or the band stops */
    name: string | null;
    kind: TimelineChord['symbol']['kind'];
    degree: string | null;
    pitches: number[];
    /** the notes as written, with their octaves ("A2", "C#4"), when the analysis writes them */
    writtenNotes: string[] | null;
}

export interface Run {
    chord: Chord;
    members: Chord[];
}

/** A part of a beat: where it begins in its beat (the j-th of n), when, and what sounds or is written there. */
export interface Part {
    at: [number, number];
    time: number;
    chord: Chord | null;
    run: Run | null;
    /** what is written where no chord is: "N.C." or a stop; null where nothing is (before the first chord) */
    silence: 'noChord' | 'stop' | null;
}

export interface Bar {
    index: number;
    /** the bar's number in the song, from 1 */
    number: number;
    start: number;
    end: number;
    beats: number[];
    timeSignature: string;
    tempo: TimelineBar['tempo'] | null;
    key: string | null;
    fermatas: number[];
    free: boolean;
    /** a short first or last bar: how many of its meter's beats it has not, and at which end */
    lacks: { beats: number; end: 'start' | 'end' } | null;
    /** for each beat, its parts in order */
    parts: Part[][];
}

export interface Section {
    index: number;
    start: number;
    end: number;
    /** the bars it spans, by index */
    from: number;
    to: number;
    name: string;
    label: TimelineSection['label'] | null;
}

/** Where something changes, at the start of a bar: the key, the meter, the tempo. The first bar has them all. */
export interface Change {
    bar: number;
    time: number;
    key: string | null | undefined;
    timeSignature: string | undefined;
    bpm: number | undefined;
}

export interface Piece {
    duration: number;
    bars: Bar[];
    chords: Chord[];
    sections: Section[];
    changes: Change[];
    /** every beat, with whether it begins a bar */
    beats: { t: number; down: boolean }[];
}

const EDGE = 0.001;

/** How many beats a whole bar of a meter has: n/d n of them, 6/8 9/8 12/8 their dotted groups, a sum its groups. */
function beatsOfMeter(timeSignature: string, written: number): number {
    if (timeSignature === 'free') return written;
    const [top = ''] = timeSignature.split('/');
    if (top.includes('+')) return top.split('+').length;
    const n = Number(top);
    return [6, 9, 12].includes(n) ? n / 3 : n;
}

export function pieceOf(timeline: Timeline): Piece {
    const given = timeline.bars, last = given.length - 1;
    const bars: Bar[] = given.map((bar, i) => {
        const free = bar.timeSignature === 'free', whole = beatsOfMeter(bar.timeSignature, bar.beats.length);
        const short = !free && bar.beats.length < whole && (i === 0 || i === last);
        return {
            index: i, number: i + 1, start: bar.start, end: bar.end, beats: bar.beats.length ? bar.beats : [bar.start],
            timeSignature: bar.timeSignature, tempo: free ? null : bar.tempo ?? null, key: bar.key ?? null, fermatas: bar.fermatas, free,
            lacks: short ? { beats: whole - bar.beats.length, end: i === 0 && last > 0 ? 'start' : 'end' } : null,
            parts: [],
        };
    });

    const chords: Chord[] = timeline.chords.map((c, index) => ({
        index, start: c.start, end: c.end, name: c.symbol.kind === 'chord' ? c.symbol.name ?? null : null,
        kind: c.symbol.kind, degree: c.degree ?? null, pitches: c.pitches, writtenNotes: c.writtenNotes ?? null,
    }));

    const placed = timeline.chords.length > 0 && timeline.chords.every(c => c.place && c.place.bar < bars.length);
    const parts = placed ? partsByPlaces(bars, timeline.chords, chords) : chordsOnBeats(bars, chords);
    bars.forEach((bar, i) => { bar.parts = parts[i] ?? []; });

    const beats = bars.flatMap(bar => bar.free ? [] : bar.beats.map((t, k) => ({ t, down: k === 0 && !(bar.lacks?.end === 'start') })));

    return { duration: timeline.duration, bars, chords, sections: sectionsOnBars(timeline.sections, bars), changes: changesOf(bars), beats };
}

// ---- by places ----------------------------------------------------------------------------------------

function partsByPlaces(bars: Bar[], given: TimelineChord[], chords: Chord[]): Part[][][] {
    // every chord at its place, in order
    const things = given.map((c, i) => {
        const place = c.place!, bar = bars[place.bar]!, k = Math.min(place.beat - 1, bar.beats.length - 1);
        const at: [number, number] = place.at ? [place.at.numerator, place.at.denominator] : [0, 1];
        return { chord: chords[i]!, bar: place.bar, beat: k, at };
    });
    let sounding: typeof things[number] | null = null;
    const runs = new Map<typeof things[number], Run | null>();
    // (every chord written is a run of its own — one written again is struck again; a chord held over a bar line is
    // written nowhere, and goes on in its run)
    const runOf = (thing: typeof things[number]): Run | null => (thing.chord.name ? { chord: thing.chord, members: [thing.chord] } : null);
    const part = (thing: typeof things[number] | null, at: [number, number], time: number): Part => (thing && thing.chord.name
        ? { at, time, chord: thing.chord, run: runs.get(thing) ?? null, silence: null }
        : { at, time, chord: null, run: null, silence: thing ? (thing.chord.kind === 'stop' ? 'stop' : 'noChord') : null });
    let next = 0;
    return bars.map((bar, b) => bar.beats.map((t0, k) => {
        const end = k + 1 < bar.beats.length ? bar.beats[k + 1]! : bar.end;
        const here: typeof things = [];
        while (next < things.length && things[next]!.bar === b && things[next]!.beat === k) {
            runs.set(things[next]!, runOf(things[next]!));
            here.push(things[next++]!);
        }
        const out = here.length && here[0]!.at[0] === 0 ? [] : [part(sounding, [0, 1], t0)];
        for (const thing of here) out.push(part(thing, thing.at, t0 + thing.at[0] / thing.at[1] * (end - t0)));
        if (here.length) sounding = here[here.length - 1]!;
        return out;
    }));
}

// ---- by times -----------------------------------------------------------------------------------------

/**
 * Which chords every beat has when the chords have times only. Every change goes to the beat nearest to it. Where
 * several changes are nearest one beat, the chord heard longest from that beat to the next has it — of theirs and the
 * one sounding before them, so a chord far shorter than a beat takes no beat from its neighbours. The others share a
 * beat with it, in their order: those before it the beat before (after what that beat has), unless that would write one
 * of them earlier than the half beat nearest to where it begins — then they share its own beat, before it; those after
 * it share its beat, after it. A change after the last beat's middle shares that beat after what it has. So no chord is
 * lost, none is written a beat late, nor earlier than it sounds by more than to its nearest half beat.
 */
function chordsOnBeats(bars: Bar[], chords: Chord[]): Part[][][] {
    interface Change { t: number; run: (Run & { start: number; end: number }) | null; until: number }
    const spans: { start: number; end: number; parts: Change[] }[] = [];
    bars.forEach(bar => bar.beats.forEach((b, i) => spans.push({ start: b, end: i + 1 < bar.beats.length ? bar.beats[i + 1]! : bar.end, parts: [] })));
    if (!spans.length) return [];
    const last = spans.length - 1;
    const spanAt = (t: number) => {
        let lo = 0, hi = last;
        while (lo < hi) {
            const m = (lo + hi + 1) >> 1;
            if (spans[m]!.start <= t) lo = m;
            else hi = m - 1;
        }
        return lo;
    };
    // the chords as runs of one name; a silence shorter than a quarter of its beat is no silence
    const runs: (Run & { start: number; end: number })[] = [];
    for (const c of chords) {
        if (!c.name) continue;
        const prev = runs[runs.length - 1];
        if (prev && c.start - prev.end < (spans[spanAt(prev.end)]!.end - spans[spanAt(prev.end)]!.start) / 4) {
            prev.end = c.start;
            if (prev.chord.name === c.name) {
                prev.end = c.end;
                prev.members.push(c);
                continue;
            }
        }
        runs.push({ chord: c, members: [c], start: c.start, end: c.end });
    }
    const changes: Change[] = [];
    runs.forEach((run, i) => {
        const nextStart = runs[i + 1]?.start ?? Infinity;
        changes.push({ t: run.start, run, until: Math.min(run.end, nextStart) });
        if (nextStart > run.end) changes.push({ t: run.end, run: null, until: nextStart });
    });
    const placeOf = (t: number) => {
        const first = spans[0]!;
        if (t < first.start) return first.start - t < (first.end - first.start) / 2 ? 0 : -1;
        const k = spanAt(t), mid = (spans[k]!.start + spans[k]!.end) / 2;
        return t < mid ? k : k + 1;
    };
    const nearestHalf = (t: number) => {
        if (t <= spans[0]!.start) return spans[0]!.start;
        const s = spans[spanAt(t)]!, near = [s.start, (s.start + s.end) / 2, s.end];
        return near.reduce((a, b) => (Math.abs(b - t) < Math.abs(a - t) ? b : a));
    };
    const heardIn = (change: Change, from: number, to: number) => Math.max(0, Math.min(change.until, to) - Math.max(change.t, from));
    let sounding: Change | null = null, k = 0;
    const fill = (upTo: number) => {
        for (; k < upTo; k++) spans[k]!.parts.push({ t: -Infinity, run: sounding ? sounding.run : null, until: -Infinity });
    };
    const canTake = (s: typeof spans[number], more: Change[]) => {
        const all = s.parts.concat(more), n = all.length;
        return all.every((c, j) => j === 0 || s.start + j / n * (s.end - s.start) >= nearestHalf(c.t) - 1e-6);
    };
    for (let a = 0; a < changes.length;) {
        const q = placeOf(changes[a]!.t);
        let b = a;
        while (b < changes.length && placeOf(changes[b]!.t) === q) b++;
        const group = changes.slice(a, b);
        a = b;
        if (q < 0) {
            sounding = group[group.length - 1]!;
            continue;
        }
        if (q > last) {
            fill(last + 1);
            for (const c of group) if (c.run && c.t < spans[last]!.end) spans[last]!.parts.push(c);
        } else {
            fill(q);
            const s = spans[k]!, had: Change = sounding ?? { t: -Infinity, run: null, until: group[0]!.t };
            let best = -1, most = heardIn(had, s.start, s.end);
            group.forEach((c, i) => {
                const heard = heardIn(c, s.start, s.end);
                if (heard >= most) {
                    most = heard;
                    best = i;
                }
            });
            const before = best < 0 ? [] : group.slice(0, best), main = best < 0 ? had : group[best]!, after = group.slice(best + 1);
            const back = k > 0 && before.length > 0 && canTake(spans[k - 1]!, before);
            if (back) spans[k - 1]!.parts = spans[k - 1]!.parts.concat(before);
            s.parts = (back ? [] : before).concat([main], after);
            k++;
        }
        sounding = group[group.length - 1]!;
    }
    fill(last + 1);
    let i = 0;
    return bars.map(bar => bar.beats.map(() => {
        const s = spans[i++]!, n = s.parts.length;
        return s.parts.map((c, j) => ({
            at: [j, n] as [number, number], time: s.start + j / n * (s.end - s.start), run: c.run, chord: c.run ? c.run.chord : null, silence: null,
        }));
    }));
}

// ---- sections and changes -----------------------------------------------------------------------------

/** The sections on the bar lines: each boundary on the line nearest to it; one left with no bar of its own is gone. */
function sectionsOnBars(sections: TimelineSection[], bars: Bar[]): Section[] {
    if (!bars.length) return [];
    const lines = [...bars.map(bar => bar.start), bars[bars.length - 1]!.end];
    const nearest = (t: number) => lines.reduce((best, line, i) => (Math.abs(line - t) < Math.abs(lines[best]! - t) ? i : best), 0);
    const out: Section[] = [];
    sections.forEach(section => {
        // (a section with no name is one the chord sheet does not head: not shown)
        if (!section.name) return;
        const from = nearest(section.start), to = nearest(section.end);
        if (to <= from) return;
        const prev = out[out.length - 1];
        if (prev && prev.to > from) prev.to = from;
        out.push({ index: out.length, start: lines[from]!, end: lines[to]!, from, to, name: section.name, label: section.label ?? null });
    });
    return out.filter(s => s.to > s.from).map((s, index) => ({ ...s, index, end: lines[s.to]! }));
}

function changesOf(bars: Bar[]): Change[] {
    const out: Change[] = [];
    let key: string | null | undefined, meter: string | undefined, bpm: number | undefined;
    bars.forEach((bar, i) => {
        const tempo = bar.tempo ? Math.round(bar.tempo.bpm) : undefined;
        const change: Change = {
            bar: i, time: bar.start,
            key: i === 0 || bar.key !== key ? bar.key : undefined,
            timeSignature: i === 0 || bar.timeSignature !== meter ? bar.timeSignature : undefined,
            bpm: tempo !== undefined && (i === 0 || tempo !== bpm) ? tempo : undefined,
        };
        if (i === 0 || change.key !== undefined || change.timeSignature !== undefined || change.bpm !== undefined) out.push(change);
        key = bar.key;
        meter = bar.timeSignature;
        if (tempo !== undefined) bpm = tempo;
    });
    return out;
}

/** The bar that sounds at time t (−1 before the first, or after the last). */
export function barAt(piece: Piece, t: number): number {
    const bars = piece.bars;
    let lo = 0, hi = bars.length - 1;
    if (!bars.length || t < bars[0]!.start - EDGE || t >= bars[hi]!.end) return -1;
    while (lo < hi) {
        const m = (lo + hi + 1) >> 1;
        if (bars[m]!.start <= t + EDGE) lo = m;
        else hi = m - 1;
    }
    return lo;
}

/** The chord that sounds at time t (−1: none). */
export function chordAt(piece: Piece, t: number): number {
    const chords = piece.chords;
    let lo = 0, hi = chords.length - 1;
    if (!chords.length || t < chords[0]!.start - EDGE) return -1;
    while (lo < hi) {
        const m = (lo + hi + 1) >> 1;
        if (chords[m]!.start <= t + EDGE) lo = m;
        else hi = m - 1;
    }
    return t < chords[lo]!.end ? lo : -1;
}

/** The section that sounds at time t (−1: none). */
export function sectionAt(piece: Piece, t: number): number {
    return piece.sections.findIndex(s => t >= s.start - EDGE && t < s.end);
}
