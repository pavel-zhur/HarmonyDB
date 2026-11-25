// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of the MPL was not
// distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.
// (FastSeekStretch below is a changed form of the overlap search of @soundtouchjs/core, which is under the MPL-2.0.)
//
// The tempo's stretcher, its signal path: free of Web Audio, so it runs in the audio worklet.
//
//   input ──► SoundTouch WSOLA time-stretch (tempo 1/ratio) ──► windowed-sinc resampler (rate ratio) ──► output
//
// The stretch makes the sound `ratio` times longer at the same pitch; the resampler plays that `ratio` times faster.
// Length is back where it was, pitch is multiplied by `ratio`. The player plays its sources at the tempo (which moves
// their pitch too) and sets the ratio to 1 / tempo: the pitch comes back where it was.
//
// What this adds to SoundTouch:
//   - a constant, known delay from input to output (LATENCY_SECONDS), held by a small servo on the stretch tempo, so
//     the player can line everything else up with the stretched music;
//   - a resampler that keeps its history across blocks and limits the band when it plays faster;
//   - a fixed order of the two stages, so crossing ratio 1 does not strand samples between them;
//   - silence in, silence out: after a pause nothing stale is left inside.

import { Stretch } from '@soundtouchjs/core';
import { LATENCY_SECONDS, MAX_RATIO, MIN_RATIO } from './stretchConstants';

// ---- resampler --------------------------------------------------------------------------------------

const ZERO_CROSSINGS = 16;   // half-width of the sinc, in zero crossings
const TABLE_RES = 512;       // table points per zero crossing
const KAISER_BETA = 9;

function besselI0(x: number): number {
    let sum = 1, term = 1;
    for (let k = 1; k < 40; k++) {
        term *= (x / (2 * k)) * (x / (2 * k));
        sum += term;
        if (term < sum * 1e-12) break;
    }
    return sum;
}

const SINC_TABLE = (() => {
    const n = ZERO_CROSSINGS * TABLE_RES;
    const table = new Float32Array(n + 2);
    const norm = besselI0(KAISER_BETA);
    for (let i = 0; i <= n; i++) {
        const x = i / TABLE_RES;
        const sinc = x === 0 ? 1 : Math.sin(Math.PI * x) / (Math.PI * x);
        const w = i / n;
        table[i] = sinc * besselI0(KAISER_BETA * Math.sqrt(1 - w * w)) / norm;
    }
    return table;
})();

// History kept behind the read position: the widest kernel the pipe can ask for.
const HISTORY_PAD = Math.ceil(ZERO_CROSSINGS * MAX_RATIO) + 2;

/**
 * Reads a stream at a varying rate. Every pushed run of frames carries the rate it is to be read at, so a change of
 * ratio takes effect exactly where the stretch began to work for it and the length of what is already inside does not
 * change.
 */
class Resampler {
    private buf = new Float32Array(2 * 16384);
    private readonly runStart: number[] = [];      // frame where each run begins
    private readonly runRate: number[] = [];
    private len = 0;                               // frames held
    private pos = 0;                               // read position, frames, fractional
    pendingOut = 0;                                // output frames still to come from what is held

    constructor() {
        this.reset();
    }

    reset(): void {
        this.buf.fill(0, 0, 2 * HISTORY_PAD);
        this.len = HISTORY_PAD;
        this.pos = HISTORY_PAD;
        this.pendingOut = 0;
        this.runStart.length = 0;
        this.runRate.length = 0;
    }

    push(interleaved: Float32Array, frames: number, rate: number): void {
        const need = 2 * (this.len + frames);
        if (need > this.buf.length) {
            const grown = new Float32Array(Math.max(need, this.buf.length * 2));
            grown.set(this.buf.subarray(0, 2 * this.len));
            this.buf = grown;
        }
        this.buf.set(interleaved.subarray(0, 2 * frames), 2 * this.len);
        if (this.runRate.length === 0 || this.runRate[this.runRate.length - 1] !== rate) {
            this.runStart.push(this.len);
            this.runRate.push(rate);
        }
        this.len += frames;
        this.pendingOut += frames / rate;
    }

    /** Writes up to `capacity` interleaved frames to `out`. Returns the frames written. */
    produce(out: Float32Array, capacity: number): number {
        const buf = this.buf, table = SINC_TABLE, limit = ZERO_CROSSINGS * TABLE_RES;
        const runStart = this.runStart, runRate = this.runRate;
        let pos = this.pos, written = 0;
        while (written < capacity && runRate.length > 0) {
            while (runStart.length > 1 && runStart[1]! <= pos) {
                runStart.shift();
                runRate.shift();
            }
            const rate = runRate[0]!;
            const runEnd = runStart.length > 1 ? runStart[1]! : Infinity;
            const scale = rate > 1 ? 1 / rate : 1;           // kernel stretch: the cutoff follows the lower of the two rates
            const half = Math.ceil(ZERO_CROSSINGS / scale);  // taps on each side
            const step = scale * TABLE_RES;
            const last = this.len - 1 - half;
            if (pos > last) break;
            while (pos <= last && pos < runEnd && written < capacity) {
                const centre = Math.floor(pos);
                const frac = pos - centre;
                let l = 0, r = 0;
                let tp = frac * step;                           // taps at and before the centre
                for (let j = centre; tp < limit; j--, tp += step) {
                    const i = tp | 0, w = table[i]! + (tp - i) * (table[i + 1]! - table[i]!);
                    l += buf[2 * j]! * w;
                    r += buf[2 * j + 1]! * w;
                }
                tp = (1 - frac) * step;                         // taps after the centre
                for (let j = centre + 1; tp < limit; j++, tp += step) {
                    const i = tp | 0, w = table[i]! + (tp - i) * (table[i + 1]! - table[i]!);
                    l += buf[2 * j]! * w;
                    r += buf[2 * j + 1]! * w;
                }
                out[2 * written] = l * scale;
                out[2 * written + 1] = r * scale;
                written++;
                pos += rate;
            }
        }
        this.pos = pos;
        this.pendingOut -= written;
        // drop what is no longer needed
        const drop = Math.floor(pos) - HISTORY_PAD;
        if (drop > 4096) {
            buf.copyWithin(0, 2 * drop, 2 * this.len);
            this.len -= drop;
            this.pos -= drop;
            for (let i = 0; i < runStart.length; i++) runStart[i] = runStart[i]! - drop;
        }
        return written;
    }
}

// ---- the stretch ------------------------------------------------------------------------------------

/** The buffers of the stretch as they are: interleaved stereo with a read window. */
interface Fifo {
    readonly vector: Float32Array;
    readonly startIndex: number;
    readonly endIndex: number;
    readonly frameCount: number;
    putSamples(samples: Float32Array, position?: number, numFrames?: number): void;
    receive(numFrames?: number): void;
}

/** The stretch's input as its search reads it. */
interface SearchInput {
    readonly startIndex: number;
    readSubarray(start: number, end: number): Float32Array;
}

/**
 * What of SoundTouch's stretch the pipe works with beyond its declared surface: the lengths of its sequence, search
 * window and overlap, which decide how much input it holds, and the reference its overlap search correlates with.
 */
interface StretchInternals {
    readonly inputBuffer: Fifo;
    readonly outputBuffer: Fifo;
    readonly overlapLength: number;
    readonly seekLength: number;
    readonly inputChunkSize: number;
    readonly outputChunkSize: number;
    readonly refMidBuffer: Float32Array;
    readonly refMidBufferEnergy: number;
    tempo: number;
    preCalculateCorrelationReferenceStereo(): void;
    setStretchParameters(params: { sequenceMs?: number; quickSeek?: boolean }): void;
    clearMidBuffer(): void;
    process(): void;
}

/**
 * SoundTouch's stretch with its exhaustive overlap search written to run fast.
 *
 * The search is the library's own exhaustive one: the normalised cross-correlation of the previous sequence's tail
 * with every position of the seek window, first maximum wins. The library recomputes the window's energy from scratch
 * at each position; here the energy slides by one frame per step. That brings one search from several milliseconds to
 * well under one, which a 3 ms audio block needs. The library's quick search is cheap too, but it misses the best
 * position often enough to be heard as a rough edge on steady notes.
 */
class FastSeekStretch extends Stretch {
    private seekScratch: Float32Array | null = null;

    override seekBestOverlapPosition(inputBuffer?: SearchInput): number {
        const self = this as unknown as StretchInternals;
        self.preCalculateCorrelationReferenceStereo();
        const ref = self.refMidBuffer, refEnergy = self.refMidBufferEnergy;
        const span = 2 * self.overlapLength, positions = self.seekLength;
        if (!inputBuffer) throw new Error('the stretch searched with no input');
        const start = inputBuffer.startIndex;
        const view = inputBuffer.readSubarray(start, start + 2 * positions + span);
        if (view.length < 2 * (positions - 1) + span) throw new Error('the stretch asked to search beyond its input');
        // the library's buffer is resizable, which makes element access slow; search in a plain copy
        if (!this.seekScratch || this.seekScratch.length < view.length) this.seekScratch = new Float32Array(view.length);
        const src = this.seekScratch;
        src.set(view);
        if (refEnergy <= 1e-12) return 0;
        let energy = 0;
        for (let i = 0; i < span; i++) energy += src[i]! * src[i]!;
        let best = 0, bestCorrelation = -Infinity;
        for (let p = 0; p < positions; p++) {
            const o = 2 * p;
            let correlation = -1;
            if (energy > 1e-12) {
                let dot = 0;
                for (let i = 0; i < span; i++) dot += src[o + i]! * ref[i]!;
                correlation = dot / Math.sqrt(energy * refEnergy);
            }
            if (correlation > bestCorrelation) {
                bestCorrelation = correlation;
                best = p;
            }
            if (p + 1 < positions) {
                const a = src[o]!, b = src[o + 1]!, c = src[o + span]!, d = src[o + span + 1]!;
                energy += c * c + d * d - a * a - b * b;
                if (energy < 1e-9) {              // too small to trust a running sum
                    energy = 0;
                    for (let i = 0; i < span; i++) energy += src[o + 2 + i]! * src[o + 2 + i]!;
                }
            }
        }
        return best;
    }
}

// ---- the pipe ---------------------------------------------------------------------------------------

const SERVO_GAIN = 8;            // 1/s: a latency error decays with an eighth-of-a-second time constant
const SERVO_MAX = 0.08;          // at most 8 % off the nominal stretch tempo
const SERVO_STEP = 0.005;        // tempo corrections move in these steps
const SERVO_DEADBAND = 0.001;    // seconds of latency error left alone
const HEADROOM_SECONDS = 0.03;   // latency kept free for the error a ratio change causes before the servo removes it

export class StretchPipe {
    private readonly targetFrames: number;
    private readonly budgetFrames: number;
    private readonly idleAfter: number;
    private readonly stretch: StretchInternals;
    private readonly resampler = new Resampler();
    private inScratch = new Float32Array(2 * 128);
    private readonly midScratch = new Float32Array(2 * 1024);
    private readonly ring = new Float32Array(2 * 2048);      // interleaved output frames waiting to be played
    private readonly ringFrames = this.ring.length / 2;
    underruns = 0;
    private ratio = 1;                                         // the ratio the stretch is working at
    private readonly changes: { at: number; ratio: number }[] = [];   // ratio changes waiting for their place in the input
    private pushed = 0;
    private pad = 0;
    private startAt = Infinity;
    private ringRead = 0;
    private ringCount = 0;
    private running = false;
    private silentRun = 0;
    private idle = true;
    private correction = 0;
    private appliedTempo = 0;

    constructor(private readonly sampleRate: number) {
        this.targetFrames = Math.round(LATENCY_SECONDS * sampleRate);
        this.budgetFrames = this.targetFrames - Math.round(HEADROOM_SECONDS * sampleRate);
        this.idleAfter = this.targetFrames + Math.round(0.5 * sampleRate);
        this.stretch = new FastSeekStretch({ sampleRate, createBuffers: true }) as unknown as StretchInternals;
        this.reset();
    }

    /** Drops everything inside. What comes in next starts from silence, LATENCY_SECONDS later. */
    flush(): void {
        if (this.changes.length > 0) this.ratio = this.changes[this.changes.length - 1]!.ratio;
        this.reset();
    }

    private reset(): void {
        this.changes.length = 0;
        this.pushed = 0;               // input frames taken since the reset
        this.pad = 0;                  // frames of silence put in front of them (see process)
        this.startAt = Infinity;       // how many input frames in the first output frame goes out
        this.stretch.inputBuffer.receive();
        this.stretch.outputBuffer.receive();
        this.stretch.clearMidBuffer();
        this.resampler.reset();
        this.ringRead = 0;
        this.ringCount = 0;
        this.running = false;          // false while the first LATENCY_SECONDS of silence go out
        this.silentRun = 0;
        this.idle = true;
        this.correction = 0;
        this.appliedTempo = 0;
    }

    /**
     * The most the pipe can hold between its input and its output ring, in output frames, with the stretch's present
     * settings: reached just before the stretch has enough input for a sequence.
     */
    private worstHoldFrames(blockFrames: number): number {
        const s = this.stretch;
        const head = s.seekLength / 2 + (s.outputChunkSize / 2) * (1 - s.tempo);
        return s.inputChunkSize - head + 2 * blockFrames;
    }

    /**
     * Sets the stretch tempo. SoundTouch picks the sequence length from the tempo; where that would need more input in
     * hand than the latency allows, the sequence is shortened until it fits.
     */
    private applyTempo(tempo: number, blockFrames: number): void {
        const s = this.stretch;
        s.setStretchParameters({ sequenceMs: 0 });
        s.tempo = tempo;
        if (this.worstHoldFrames(blockFrames) > this.budgetFrames) {
            const overlap = s.overlapLength, seek = s.seekLength;
            const room = this.budgetFrames - 2 * blockFrames - overlap - seek / 2 - 2;   // 2: the stretch rounds its lengths
            const chunk = room / (tempo >= 1 ? 1.5 * tempo - 0.5 : 0.5 + 0.5 * tempo);
            const sequenceMs = 1000 * (Math.floor(chunk) + overlap) / this.sampleRate;
            s.setStretchParameters({ sequenceMs });
            if (this.worstHoldFrames(blockFrames) > this.budgetFrames) throw new Error(`no sequence length fits the latency at stretch tempo ${tempo}`);
        }
        this.appliedTempo = tempo;
    }

    /** The ratio (output frequency / input frequency) for the input from here on. What is inside keeps its own. */
    setRatio(ratio: number): void {
        if (!(ratio >= MIN_RATIO && ratio <= MAX_RATIO)) throw new RangeError(`ratio ${ratio} is outside ${MIN_RATIO}…${MAX_RATIO}`);
        if (this.idle) {
            this.ratio = ratio;
            return;
        }
        const last = this.changes.length > 0 ? this.changes[this.changes.length - 1]!.ratio : this.ratio;
        if (ratio !== last) this.changes.push({ at: this.pushed, ratio });
    }

    /** Where the stretch's nominal read head is, in input frames since the reset. */
    private headPosition(): number {
        const s = this.stretch;
        return this.pushed + this.pad - s.inputBuffer.frameCount + s.seekLength / 2 + (s.outputChunkSize / 2) * (1 - s.tempo);
    }

    /** Output frames played before a frame pushed now: in the ring, the resampler, and the stretch behind its head. */
    private pendingFrames(): number {
        const s = this.stretch;
        const head = s.seekLength / 2 + (s.outputChunkSize / 2) * (1 - s.tempo);
        return this.ringCount + this.resampler.pendingOut + (s.inputBuffer.frameCount - head);
    }

    /** One block. `inL`/`inR` may be null (nothing connected: silence). Writes `frames` frames to outL/outR. */
    process(inL: Float32Array | null, inR: Float32Array | null, outL: Float32Array, outR: Float32Array, frames: number): void {
        let silent = true;
        if (inL && inR) {
            for (let i = 0; i < frames; i++) {
                if (inL[i] !== 0 || inR[i] !== 0) {
                    silent = false;
                    break;
                }
            }
        }
        if (silent) {
            this.silentRun += frames;
            if (this.idle || this.silentRun >= this.idleAfter) {
                if (!this.idle) this.reset();
                outL.fill(0, 0, frames);
                outR.fill(0, 0, frames);
                return;
            }
        } else {
            this.silentRun = 0;
            this.idle = false;
        }

        // in
        if (this.inScratch.length < 2 * frames) this.inScratch = new Float32Array(2 * frames);
        const scratch = this.inScratch;
        if (silent || !inL || !inR) scratch.fill(0, 0, 2 * frames);
        else for (let i = 0; i < frames; i++) {
            scratch[2 * i] = inL[i]!;
            scratch[2 * i + 1] = inR[i]!;
        }
        const stretch = this.stretch;
        if (this.appliedTempo === 0) {
            // The first block after a reset. The stretch begins by taking one overlap of input as its "previous tail"
            // and searching the next seek window for where to go on: it would drop up to that much of the beginning.
            // Silence of that length in front lets it drop silence instead, and makes the start exact: the first frame
            // that came in goes out LATENCY_SECONDS later, to the sample.
            this.applyTempo((1 + this.correction) / this.ratio, frames);
            this.pad = stretch.overlapLength + stretch.seekLength;
            stretch.inputBuffer.putSamples(new Float32Array(2 * this.pad), 0, this.pad);
            // (the silence in front that the stretch does keep — one seek window — comes out first, played at the ratio)
            this.startAt = this.targetFrames - stretch.seekLength / this.ratio;
        }
        stretch.inputBuffer.putSamples(scratch, 0, frames);
        this.pushed += frames;

        // stretch
        while (this.changes.length > 0 && this.headPosition() >= this.changes[0]!.at) this.ratio = this.changes.shift()!.ratio;
        const tempo = (1 + this.correction) / this.ratio;
        if (tempo !== this.appliedTempo) this.applyTempo(tempo, frames);
        stretch.process();

        // resample into the ring
        const mid = stretch.outputBuffer;
        const midFrames = mid.frameCount;
        if (midFrames > 0) {
            this.resampler.push(mid.vector.subarray(mid.startIndex, mid.endIndex), midFrames, this.ratio);
            mid.receive(midFrames);
        }
        // only what the next blocks need: resampling a whole chunk at once would make one block far dearer than the rest
        const want = 2 * frames - this.ringCount;
        if (want > 0) {
            const n = this.resampler.produce(this.midScratch, Math.min(want, this.midScratch.length / 2));
            this.ringWrite(this.midScratch, n);
        }

        // out
        let take = frames;
        if (!this.running) {
            const ready = Math.floor(this.pushed - this.startAt);
            if (ready <= 0) take = 0;
            else {
                take = Math.min(frames, ready);
                this.running = true;
            }
        }
        if (take > this.ringCount) {
            this.underruns++;
            take = this.ringCount;
        }
        const lead = frames - take;      // silence goes first: what is played ends on the block's last frame
        outL.fill(0, 0, lead);
        outR.fill(0, 0, lead);
        const ring = this.ring, size = this.ringFrames;
        let rd = this.ringRead;
        for (let i = lead; i < frames; i++) {
            outL[i] = ring[2 * rd]!;
            outR[i] = ring[2 * rd + 1]!;
            if (++rd === size) rd = 0;
        }
        this.ringRead = rd;
        this.ringCount -= take;

        // hold the latency
        if (this.running) {
            const error = (this.pendingFrames() - this.targetFrames) / this.sampleRate;
            const wanted = Math.abs(error) < SERVO_DEADBAND ? 0 : Math.max(-SERVO_MAX, Math.min(SERVO_MAX, SERVO_GAIN * error));
            this.correction = Math.round(wanted / SERVO_STEP) * SERVO_STEP;
        }
    }

    private ringWrite(interleaved: Float32Array, frames: number): void {
        if (this.ringCount + frames > this.ringFrames) throw new Error('the stretcher\'s output ring overflowed');
        const ring = this.ring, size = this.ringFrames;
        let wr = this.ringRead + this.ringCount;
        if (wr >= size) wr -= size;
        for (let i = 0; i < frames; i++) {
            ring[2 * wr] = interleaved[2 * i]!;
            ring[2 * wr + 1] = interleaved[2 * i + 1]!;
            if (++wr === size) wr = 0;
        }
        this.ringCount += frames;
    }
}
