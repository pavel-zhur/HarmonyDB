// Scheduled sounds: things played at song times (metronome clicks, the chords). A lane owns a mixer channel and a
// provider that says what sounds in a stretch of song time; the player's scheduler asks a little ahead of the music
// and hands each sound the exact context time to start at.

import type { ChannelStrip } from './mixer';
import type { Timeline } from './timeline';

const CUT_FADE = 0.005;      // a sound cut short fades over this long, seconds

/** tempo: song seconds per second (durations in song time divide by it); context: the context to build in */
export interface LaneInfo {
    tempo: number;
    context: BaseAudioContext;
}

/** A sound at a song time: `start` builds it at context time `when` into `destination` and returns the nodes started. */
export interface LaneEvent {
    time: number;
    start(when: number, destination: AudioNode, info: LaneInfo): AudioScheduledSourceNode[];
}

/** (from, to) → the events with `from <= time < to` (song seconds). */
export type LaneProvider = (from: number, to: number) => Iterable<LaneEvent>;

interface Voice {
    when: number;
    time: number;
    gain: GainNode;
    nodes: AudioScheduledSourceNode[];
    cutAt: number | null;
    left: number;
}

export class Lane {
    provider: LaneProvider | null = null;
    readonly voices = new Set<Voice>();
    /** context time up to which the lane is scheduled */
    private cursor = -Infinity;

    constructor(private readonly context: BaseAudioContext, readonly name: string, readonly channel: ChannelStrip) {}

    /** Sets (or with null, removes) the provider. What was scheduled and has not started is withdrawn. */
    setProvider(provider: LaneProvider | null): void {
        this.provider = provider;
        this.invalidate();
    }

    /** Call when the provider's answers have changed (other beats, other chords). */
    invalidate(): void {
        const now = this.context.currentTime;
        for (const voice of [...this.voices]) if (voice.when > now) this.cancel(voice);
        this.cursor = -Infinity;
    }

    // ---- used by the player ---------------------------------------------------------------------------

    schedule(now: number, horizon: number, timeline: Timeline): void {
        if (!this.provider || !this.channel.audible) {
            if (this.cursor > now) this.invalidate();
            return;
        }
        const from = Math.max(this.cursor, now);
        if (from >= horizon) return;
        const entries = timeline.entries;
        for (let i = 0; i < entries.length; i++) {
            const e = entries[i]!;
            const a = Math.max(from, e.t), b = Math.min(horizon, i + 1 < entries.length ? entries[i + 1]!.t : Infinity);
            if (b <= a || e.rate === 0) continue;
            const s0 = e.pos + (a - e.t) * e.rate, s1 = e.pos + (b - e.t) * e.rate;
            const info: LaneInfo = { tempo: e.rate, context: this.context };
            for (const event of this.provider(s0, s1)) {
                if (!(event.time >= s0 && event.time < s1)) throw new RangeError(`lane "${this.name}": an event at ${event.time} is outside ${s0}…${s1}`);
                this.startVoice(event, e.t + (event.time - e.pos) / e.rate, info, timeline);
            }
        }
        // Scheduled up to the horizon — but only as far as the timeline is known. At rest nothing is known beyond now:
        // what starts next (the music) may begin before the horizon, and must be asked for from its beginning.
        this.cursor = timeline.last.rate === 0 ? Math.min(horizon, now) : horizon;
    }

    private startVoice(event: LaneEvent, when: number, info: LaneInfo, timeline: Timeline): void {
        const gain = this.context.createGain();
        gain.connect(this.channel.input);
        const nodes = event.start(when, gain, info);
        if (nodes.length === 0) {
            gain.disconnect();
            return;
        }
        const voice: Voice = { when, time: event.time, gain, nodes, cutAt: null, left: nodes.length };
        for (const node of nodes) node.addEventListener('ended', () => { if (--voice.left === 0) this.drop(voice); }, { once: true });
        this.voices.add(voice);
        const cut = timeline.nextCutAfter(when);
        if (cut) this.applyCut(voice, cut.t);
    }

    private applyCut(voice: Voice, t: number): void {
        if (voice.cutAt !== null) {
            if (voice.cutAt <= t) return;
            voice.gain.gain.cancelScheduledValues(voice.cutAt - 1e-4);
        }
        voice.gain.gain.setValueAtTime(1, t);
        voice.gain.gain.linearRampToValueAtTime(0, t + CUT_FADE);
        voice.cutAt = t;
    }

    /** The sound is cut at context time `t`: what sounds fades there, what would start later does not. */
    cutAt(t: number): void {
        for (const voice of [...this.voices]) {
            if (voice.when >= t) this.cancel(voice);
            else this.applyCut(voice, t);
        }
        if (this.cursor > t) this.cursor = t;
    }

    /** The timeline after `t` was withdrawn: so is what was scheduled there, and cuts that no longer exist. */
    truncate(t: number, timeline: Timeline): void {
        for (const voice of [...this.voices]) {
            if (voice.when >= t) {
                this.cancel(voice);
                continue;
            }
            if (voice.cutAt !== null && voice.cutAt > t) {
                voice.gain.gain.cancelScheduledValues(voice.cutAt - 1e-4);
                voice.cutAt = null;
                const cut = timeline.nextCutAfter(voice.when);
                if (cut) this.applyCut(voice, cut.t);
            }
        }
        if (this.cursor > t) this.cursor = t;
    }

    /** Stops what has faded out. */
    sweep(now: number): void {
        for (const voice of [...this.voices]) {
            if (voice.cutAt !== null && now > voice.cutAt + CUT_FADE + 0.02) this.cancel(voice);
        }
    }

    private cancel(voice: Voice): void {
        for (const node of voice.nodes) node.stop();
        this.drop(voice);
    }

    private drop(voice: Voice): void {
        voice.gain.disconnect();
        this.voices.delete(voice);
    }

    dispose(): void {
        for (const voice of [...this.voices]) this.cancel(voice);
        this.provider = null;
    }
}
