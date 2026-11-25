// The mixing graph: one strip per channel, the music's two decks with the tempo's stretcher, the master.
//
//                      ┌─ deck 0 bus ─┬─ dry ─────────────────────────────┐
//   music strips ──────┤              └─ to the stretcher ─► gate ────────┼─► master ─► destination
//   (a chain per deck) └─ deck 1 bus ─┴─ (the same two)                   │
//                                                     lane strips ────────┘
//
// A deck is one stream of music with one way out: untouched ("dry"), or through the stretcher. There are two so that
// the music can move from one way out to the other without stopping: the new stream starts on the other deck, ahead
// by the stretcher's delay, and the two are cross-faded when the new one is heard. Every music strip therefore has two
// identical chains (a fader each), one per deck, moved together. Scheduled sounds (lanes) join after the stretcher:
// they are never stretched.
//
// With everything at its default a chain is input → fader → bus, all at gain 1, and the dry way out is a gain of 1:
// nothing touches the samples.

import type { Shifter } from './shifter';

const SMOOTH = 0.012;           // time constant of every level move, seconds

export type Route = 'dry' | 'stretch';
export type StripKind = 'music' | 'lane';

export interface ChannelState {
    gain: number;
    muted: boolean;
}

/** One signal chain of a strip: input → fader → output. */
class Chain {
    readonly input: GainNode;
    readonly fader: GainNode;

    constructor(private readonly context: BaseAudioContext, output: AudioNode) {
        this.input = context.createGain();
        this.fader = context.createGain();
        this.input.connect(this.fader);
        this.fader.connect(output);
    }

    setLevel(value: number): void {
        this.fader.gain.setTargetAtTime(value, this.context.currentTime, SMOOTH);
    }

    dispose(): void {
        this.input.disconnect();
        this.fader.disconnect();
    }
}

/**
 * One mixer channel. Read and write its properties; every change is smoothed.
 *
 *   gain    0…4      fader
 *   muted   boolean
 */
export class ChannelStrip {
    state: ChannelState = { gain: 1, muted: false };
    /** set by the graph: not muted, and in use */
    audible = true;
    readonly chains: Chain[];

    constructor(private readonly graph: Graph, readonly name: string, readonly kind: StripKind) {
        this.chains = kind === 'music'
            ? graph.decks.map(deck => new Chain(graph.context, deck.bus))
            : [new Chain(graph.context, graph.master)];
    }

    /** Where sounds for this channel are connected (a lane's one chain; a music strip's chain for deck 0). */
    get input(): GainNode {
        return this.chains[0]!.input;
    }

    /** A music strip's input for a deck. */
    inputOf(deck: number): GainNode {
        return this.chains[deck]!.input;
    }

    get gain(): number {
        return this.state.gain;
    }

    set gain(value: number) {
        if (!(value >= 0 && value <= 4)) throw new RangeError(`gain must be 0…4, got ${value}`);
        this.state.gain = value;
        this.applyLevel();
        this.changed();
    }

    get muted(): boolean {
        return this.state.muted;
    }

    set muted(value: boolean) {
        this.state.muted = value;
        this.graph.updateAudibility();
    }

    setAudible(audible: boolean): void {
        if (audible === this.audible) return;
        this.audible = audible;
        this.applyLevel();
        this.changed();
    }

    private applyLevel(): void {
        for (const chain of this.chains) chain.setLevel(this.audible ? this.state.gain : 0);
    }

    private changed(): void {
        if (this.kind === 'music') this.graph.onMusicChange();
    }

    dispose(): void {
        for (const chain of this.chains) chain.dispose();
    }
}

export interface Deck {
    bus: GainNode;
    sends: Record<Route, GainNode>;
}

export class Graph {
    readonly strips = new Map<string, ChannelStrip>();
    readonly master: GainNode;
    /** the gain after the stretcher (used to silence its output at a cut) */
    readonly gate: GainNode;
    readonly decks: Deck[] = [];
    /** which music strips are in use; set by the player */
    active: (name: string) => boolean = () => true;
    /** called when the sound of the music changes by the mixer */
    onMusicChange: () => void = () => {};

    constructor(readonly context: BaseAudioContext, shifter: Shifter) {
        this.master = context.createGain();
        this.master.connect(context.destination);
        this.gate = context.createGain();
        shifter.node.connect(this.gate);
        this.gate.connect(this.master);
        for (let d = 0; d < 2; d++) {
            const bus = context.createGain();
            const sends: Record<Route, GainNode> = { dry: context.createGain(), stretch: context.createGain() };
            bus.connect(sends.dry);
            sends.dry.connect(this.master);
            bus.connect(sends.stretch);
            sends.stretch.connect(shifter.node);
            sends.dry.gain.value = 0;
            sends.stretch.gain.value = 0;
            this.decks.push({ bus, sends });
        }
    }

    addStrip(name: string, kind: StripKind): ChannelStrip {
        if (this.strips.has(name)) throw new Error(`channel "${name}" already exists`);
        const strip = new ChannelStrip(this, name, kind);
        this.strips.set(name, strip);
        return strip;
    }

    removeStrip(name: string): void {
        const strip = this.strips.get(name);
        if (!strip) throw new Error(`no channel "${name}"`);
        strip.dispose();
        this.strips.delete(name);
        this.updateAudibility();
    }

    /** Which channels in use sound: those not muted. */
    updateAudibility(): void {
        for (const s of this.strips.values()) if (s.kind === 'lane' || this.active(s.name)) s.setAudible(!s.state.muted);
    }

    dispose(): void {
        for (const strip of this.strips.values()) strip.dispose();
        this.strips.clear();
        this.master.disconnect();
        this.gate.disconnect();
        for (const deck of this.decks) {
            deck.bus.disconnect();
            for (const send of Object.values(deck.sends)) send.disconnect();
        }
    }
}
