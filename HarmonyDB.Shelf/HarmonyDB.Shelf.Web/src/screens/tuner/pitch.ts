// The pitch of a sound and its note: the YIN method (de Cheveigné and Kawahara, 2002) over one window of samples —
// the difference of the signal with itself shifted, normalised by its running mean; the first shift below a threshold
// is the period, refined between its neighbours. Pure: the tuner gives it what the microphone heard.

/** The lowest and highest pitches looked for, Hz: below a bass's low E, above a guitar's 20th fret on the high E. */
const LOWEST = 38;
const HIGHEST = 1400;
/** How deep a dip must be to be the period. */
const THRESHOLD = 0.12;
/** Quieter than this (root mean square) is silence. */
const SILENCE = 0.01;

/** The pitch of the samples, Hz; null for silence or a sound with no clear pitch. */
export function detectPitch(samples: Float32Array, sampleRate: number): number | null {
    let power = 0;
    for (let i = 0; i < samples.length; i++) power += samples[i]! * samples[i]!;
    if (Math.sqrt(power / samples.length) < SILENCE) return null;

    const maxShift = Math.min(Math.floor(sampleRate / LOWEST), Math.floor(samples.length / 2));
    const minShift = Math.max(2, Math.floor(sampleRate / HIGHEST));
    const width = samples.length - maxShift;
    const d = new Float32Array(maxShift + 1);
    for (let tau = 1; tau <= maxShift; tau++) {
        let sum = 0;
        for (let i = 0; i < width; i++) {
            const delta = samples[i]! - samples[i + tau]!;
            sum += delta * delta;
        }
        d[tau] = sum;
    }

    // the cumulative mean normalised difference
    const cmnd = new Float32Array(maxShift + 1);
    cmnd[0] = 1;
    let running = 0;
    for (let tau = 1; tau <= maxShift; tau++) {
        running += d[tau]!;
        cmnd[tau] = running === 0 ? 1 : (d[tau]! * tau) / running;
    }

    let tau = minShift;
    while (tau < maxShift && cmnd[tau]! >= THRESHOLD) tau++;
    if (tau >= maxShift) return null;
    // down to the bottom of the dip
    while (tau + 1 < maxShift && cmnd[tau + 1]! < cmnd[tau]!) tau++;

    // a parabola through the dip and its neighbours: the period between samples
    const a = cmnd[tau - 1]!, b = cmnd[tau]!, c = cmnd[tau + 1] ?? b;
    const curve = a + c - 2 * b;
    const shift = curve === 0 ? 0 : (a - c) / (2 * curve);
    return sampleRate / (tau + shift);
}

export interface Heard {
    /** MIDI number of the nearest note (A4 = 69) */
    midi: number;
    /** how far from it, cents (−50 … 50) */
    cents: number;
    frequency: number;
}

/** The nearest note of a pitch, A4 tuned to `a4` Hz. */
export function nearestNote(frequency: number, a4: number): Heard {
    const exact = 69 + 12 * Math.log2(frequency / a4);
    const midi = Math.round(exact);
    return { midi, cents: Math.round((exact - midi) * 100), frequency };
}

/** The pitch of a note, A4 tuned to `a4` Hz. */
export function noteFrequency(midi: number, a4: number): number {
    return a4 * Math.pow(2, (midi - 69) / 12);
}
