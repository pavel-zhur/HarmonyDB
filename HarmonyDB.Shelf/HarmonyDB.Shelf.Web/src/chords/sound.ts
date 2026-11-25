// The sound of a chord — the one of the app: the old frontend's plucked string (services/Player.ts with
// public/services-js/Player.js: Karplus–Strong, a string of noise averaged on itself, damped, through a bandpass on
// its note; the notes strummed 25 ms apart). Here the string is computed into a buffer, so that it can also be
// scheduled at a time of an audio context (the player plays the chords of an analysis with it):
//
//   pluckChord(context, destination, when, pitches, seconds)   // MIDI pitches, from context time `when`
//   playChord(chordData, name)                                 // a tap on a chord: it sounds now, and the fingering
//                                                              // window is told

import { useSyncExternalStore } from 'react';
import { chordDataParser } from './analysis';

/** How long a string sounds (its signal has died down by then), seconds. */
const RING = 2;
/** The strum: the next string this much later, seconds. */
const STAGGER = 0.025;
/** The damping of the string at each step. */
const DAMPING = 0.99;

const frequency = (midi: number) => 440 * Math.pow(2, (midi - 69) / 12);

/** One plucked string: noise between −1 and 1, each sample the average of itself and the next, damped. */
function string(context: BaseAudioContext, hz: number, seconds: number): AudioBuffer {
    const rate = context.sampleRate;
    const n = Math.round(rate / hz);
    const y = new Float32Array(n);
    for (let i = 0; i < n; i++) y[i] = Math.random() * 2 - 1;
    const buffer = context.createBuffer(1, Math.ceil(rate * seconds), rate);
    const output = buffer.getChannelData(0);
    let k = 0;
    for (let i = 0; i < output.length; i++) {
        y[k] = (y[k]! + y[(k + 1) % n]!) / 2;
        output[i] = y[k]!;
        y[k]! *= DAMPING;
        k++;
        if (k >= n) k = 0;
    }
    return buffer;
}

/**
 * A chord on plucked strings, its notes (MIDI pitches, the lowest first) strummed from context time `when`; each
 * string rings at most `seconds` (and at most as long as it sounds at all). The sources, to stop them sooner.
 */
export function pluckChord(context: BaseAudioContext, destination: AudioNode, when: number, pitches: readonly number[], seconds = RING): AudioScheduledSourceNode[] {
    const length = Math.min(Math.max(seconds, 0.05), RING);
    return pitches.map((midi, i) => {
        const hz = frequency(midi);
        const source = context.createBufferSource();
        source.buffer = string(context, hz, length);
        // the aliasing noise of the low notes taken away: a bandpass on the note
        const bandpass = context.createBiquadFilter();
        bandpass.type = 'bandpass';
        bandpass.frequency.value = hz;
        bandpass.Q.value = 1;
        source.connect(bandpass);
        bandpass.connect(destination);
        source.addEventListener('ended', () => bandpass.disconnect(), { once: true });
        source.start(when + STAGGER * i);
        return source;
    });
}

/** A chord's notes by its data ("[!0!]m7"): two of its bass (A2 is the old player's lowest A), then the chord above. */
export function chordPitches(chordData: string): number[] | null {
    const notes = chordDataParser.getNotes(chordData);
    // the old player's note 0 is A at 110 Hz: MIDI 45
    return notes === null ? null : [...notes.bass, ...notes.main].map(x => 45 + x);
}

export interface PlayedChord {
    /** the chord's data ("[!0!]m7") */
    readonly data: string;
    /** what the chord was written as, for the window's title */
    readonly name: string;
    /** the root (A is 0) and the chord's type as the fingerings know it ("m7"), when it has one */
    readonly note: number;
    readonly fingering: string | null;
}

let context: AudioContext | null = null;
let played: PlayedChord | null = null;
const listeners = new Set<() => void>();

/** Plays a chord by its data now and tells the fingering window. */
export function playChord(data: string, name: string): void {
    const parsed = chordDataParser.getNotes(data);
    if (parsed === null) return;

    // the sound first: the window is drawn after it
    context ??= new AudioContext();
    const audio = context;
    const pitches = [...parsed.bass, ...parsed.main].map(x => 45 + x);
    void audio.resume().then(() => pluckChord(audio, audio.destination, audio.currentTime, pitches));

    played = { data, name, note: parsed.root, fingering: parsed.fingering };
    listeners.forEach(listener => listener());
}

/** The chord played last (null: none, or the window closed). */
export function usePlayedChord(): PlayedChord | null {
    return useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => played);
}

export function forgetPlayedChord(): void {
    played = null;
    listeners.forEach(listener => listener());
}
