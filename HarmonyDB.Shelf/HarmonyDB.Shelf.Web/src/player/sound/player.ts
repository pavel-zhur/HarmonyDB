// The sound of an analysis: its recording or its separate instruments, a transport, the tempo, a loop, a
// mixer, scheduled sounds (the metronome, the chords) — all on one clock, the AudioContext's.
//
// How it keeps time
//   - The music is AudioBufferSourceNodes. All sources of one start share the same start time and offset, so the
//     instruments are locked to the sample. The tempo is their playbackRate.
//   - When the tempo is not 1, the music goes through the stretcher (it puts the pitch back), which delays it by a
//     fixed amount. The sources then simply run that much ahead of what is heard.
//   - The music has two ways out — untouched, or through the stretcher — and two decks (mixer.ts). To change the way
//     out while playing, the new stream starts on the other deck, ahead by its delay, and takes over with a cross-fade
//     when it is heard: no stop.
//   - While paused with the stretcher wanted, it is fed its delay's worth of the music ahead of time and then held
//     still, so that play() sounds at once.
//   - `Timeline` records what is heard when. Views read the position from it; scheduled sounds are placed by it; so
//     both stay with the music at any tempo.
//   - Nothing is driven by the screen's refresh. A worker timer wakes the scheduler, which looks a little ahead: it
//     commits the next loop wrap or the song's end, and asks the lanes for the sounds that fall in the stretch ahead.
//
// Times inside: `frame` is a count of context sample frames ("source time": when a source plays something); the
// timeline's `t` is in seconds, when it is heard. heard = source + latency.

import { Timeline, type Entry, type EntryKind, type SourceGroup } from './timeline';
import { Graph, type ChannelStrip, type Route } from './mixer';
import { Lane } from './lanes';
import { createShifter, disposeShifter, shifterRatio, type Shifter } from './shifter';
import TickWorker from './tickWorker.ts?worker';
import { MAX_TEMPO, MIN_LOOP_SECONDS, MIN_TEMPO, PlayerError, type Stem } from './types';

export { MIN_TEMPO, MAX_TEMPO, MIN_LOOP_SECONDS, PlayerError, type PlayerErrorCode, type Stem } from './types';

const QUANTUM = 128;                // frames per render block
const FADE = 2 * QUANTUM;           // frames: every start, stop and cut is faded over this (5.8 ms at 44.1 kHz)
const LEAD = 0.04;                  // seconds between deciding something and the sources doing it
const SAFE = 0.015;                 // sources scheduled less than this ahead may not all start on the same sample
const START_ATTEMPTS = 3;
const NO_GESTURE_MS = 1000;         // how long play() waits for a context the browser may be holding back
const OUTPUT_START_MS = 10000;      // how long it waits for the audio output to open once sound is allowed
const ANCHOR_SECONDS = 2;           // how long a reading of the context clock is trusted to be carried forward
const PLAN_AHEAD = 0.5;             // seconds: loop wraps and the end are committed this far ahead
const LANE_AHEAD = 0.3;             // seconds: scheduled sounds are placed this far ahead
const TICK_MS = 25;
const LOOP_CROSSFADE = 0.01;        // seconds
const ROUTE_FADE = 8 * QUANTUM;     // frames: when the way out changes, the old stream fades over this under the new one
const PRIME_DELAY_MS = 60;          // after the last change while paused, the stretcher is made ready this much later
const IDLE_SUSPEND_SECONDS = 30;    // with nothing playing for this long, the audio output is let go


interface Primed {
    deck: number;
    posFrames: number;
    tempo: number;
    group: SourceGroup;
    pre: number;
    readyFrame: number;
    nextFrames: number;
}


type PlayerEvent = 'play' | 'pause' | 'seek' | 'ended' | 'loop' | 'load' | 'change' | 'error';

export class Player extends EventTarget {
    /** Makes a player with its own AudioContext, at the rate of the files. */
    static async create(sampleRate = 44100): Promise<Player> {
        const context = new AudioContext({ sampleRate, latencyHint: 'interactive' });
        try {
            const player = new Player(context);
            const shifter = await createShifter(context, error => player.raise(new PlayerError('underrun', error.message)));
            player.build(shifter);
            return player;
        } catch (error) {
            await context.close();
            throw error;
        }
    }

    readonly sampleRate: number;
    track: AudioBuffer | null = null;
    stems: Stem[] | null = null;
    private stemsPlay = false;
    private currentTempo = 1;
    private currentLoop: { start: number; end: number; pause: boolean } | null = null;
    private playing = false;

    private timeline = new Timeline();
    private groups: SourceGroup[] = [];
    private route: Route = 'dry';   // the way out of the music now playing
    private deck = 0;               // the deck it plays on
    private latencyFrames = 0;      // delay of that way out
    private epoch = 0;
    private busyUntil = 0;          // frame before which no new change may begin
    private holdUntil = 0;          // frame before which no change but a cut may begin (the way out is being changed)
    private primed: Primed | null = null;
    private primeTimer: ReturnType<typeof setTimeout> | null = null;
    private idleTimer: ReturnType<typeof setTimeout> | null = null;
    private suspendedForIdleness = false;
    private sent: { pos: number; until: number } | null = null;     // the last seek while playing
    private anchor: { offset: number; wall: number } | null = null; // the best recent reading of the context clock
    private lastOutputTime = 0;
    readonly lanes = new Map<string, Lane>();
    private disposed = false;
    private readonly ticker: Worker;
    private ticking = false;
    private shifter!: Shifter;
    private graph!: Graph;

    private constructor(readonly context: AudioContext) {
        super();
        this.sampleRate = context.sampleRate;
        this.ticker = new TickWorker();
        this.ticker.onmessage = () => this.tick();
        this.ticker.onerror = event => this.raise(new Error(`the scheduler's timer failed: ${event.message}`));
        context.addEventListener('statechange', this.onStateChange);
    }

    private readonly onStateChange = () => {
        if (this.context.state === 'running' && !this.playing) {
            if (this.primed === null) this.schedulePrime();
            this.armIdle();
        }
    };

    private build(shifter: Shifter): void {
        this.shifter = shifter;
        this.graph = new Graph(this.context, shifter);
        this.graph.active = name => this.sources().some(source => source.name === name);
        this.graph.onMusicChange = () => this.schedulePrime();
    }

    // ---- sources --------------------------------------------------------------------------------------

    /** Decodes a whole audio file. The ArrayBuffer is consumed. */
    decode(data: ArrayBuffer): Promise<AudioBuffer> {
        return this.context.decodeAudioData(data);
    }

    /** Loads a recording, replacing the one before. Playback stops, the position returns to 0, the loop is cleared; the tempo and the lanes stay. */
    async load(track: AudioBuffer): Promise<void> {
        this.check(track, 'the recording');
        await this.unload();
        this.track = track;
        this.graph.addStrip('track', 'music');
        this.graph.updateAudibility();
        this.schedulePrime();
        this.armIdle();
        this.emit('load');
    }

    /**
     * Gives the recording's separate instruments (they arrive later than the recording), replacing those before.
     * Playback goes on; they sound once `byStems` is set. The set being played cannot be replaced while it plays.
     */
    attachStems(stems: Stem[]): void {
        if (!this.track) throw new PlayerError('no-song', 'the instruments come with a loaded recording');
        if (this.playing && this.stemsPlay) throw new PlayerError('busy', 'the instruments cannot be replaced while they play');
        const names = new Set<string>();
        for (const s of stems) {
            this.check(s.buffer, `the instrument "${s.name}"`);
            if (!s.name || s.name === 'track' || names.has(s.name) || this.lanes.has(s.name)) throw new PlayerError('bad-sources', `the instrument "${s.name}" is unnamed, repeated, or its name is taken`);
            names.add(s.name);
        }
        if (this.stems) for (const s of this.stems) this.graph.removeStrip(s.name);
        this.stems = stems.map(s => ({ name: s.name, buffer: s.buffer }));
        for (const s of stems) this.graph.addStrip(s.name, 'music');
        this.graph.updateAudibility();
        this.schedulePrime();
        this.emit('load');
    }

    /** Lets the instruments go, to free their memory (the recording plays). */
    releaseStems(): void {
        if (!this.stems) return;
        this.byStems = false;
        for (const s of this.stems) this.graph.removeStrip(s.name);
        this.stems = null;
        this.emit('load');
    }

    /** Stops and forgets the recording and its instruments. */
    async unload(): Promise<void> {
        if (this.playing) this.pause();
        this.unprime();
        await this.settled();
        for (const group of [...this.groups]) this.killGroup(group);
        if (this.track) this.graph.removeStrip('track');
        if (this.stems) for (const s of this.stems) this.graph.removeStrip(s.name);
        this.track = null;
        this.stems = null;
        this.stemsPlay = false;
        this.currentLoop = null;
        this.timeline = new Timeline();
    }

    private check(buffer: AudioBuffer, what: string): void {
        if (buffer.sampleRate !== this.sampleRate) throw new PlayerError('bad-sources', `${what} is at ${buffer.sampleRate} Hz, the player at ${this.sampleRate} Hz`);
    }

    /** The buffers played — the recording, or its instruments — with their channel names. */
    private sources(): Stem[] {
        if (this.stemsPlay && this.stems) return this.stems;
        return this.track ? [{ name: 'track', buffer: this.track }] : [];
    }

    get loaded(): boolean {
        return this.track !== null;
    }

    /** Whether the instruments play in place of the recording. While playing, the position is kept and the two are cross-faded. */
    get byStems(): boolean {
        return this.stemsPlay;
    }

    set byStems(on: boolean) {
        if (on && !this.stems) throw new PlayerError('no-song', 'no instruments are loaded');
        if (on === this.stemsPlay) return;
        this.stemsPlay = on;
        this.graph.updateAudibility();
        if (this.playing) this.swapSources();
        else this.schedulePrime();
        this.emit('change');
    }

    /** Length of the music in seconds: of the set in use (an instrument may be a few samples shorter than the recording). */
    get duration(): number {
        const s = this.sources();
        return s.length ? Math.min(...s.map(x => x.buffer.length)) / this.sampleRate : 0;
    }

    // ---- transport ------------------------------------------------------------------------------------

    /**
     * Starts playing from the current position (from the start, or the loop's start, if at the end). The browser lets
     * sound begin only after a tap or a key: call this from one. Rejects with PlayerError 'gesture-required' if it does
     * not. `at`: a context time to start at (a count-in before it), when that is later than the soonest possible.
     */
    async play(at: number | null = null): Promise<void> {
        if (this.playing) return;
        if (!this.track) throw new PlayerError('no-song', 'nothing is loaded');
        await this.resume();
        if (this.playing || !this.track) return;
        this.disarmIdle();
        const posFrames = this.startFrames();
        if (this.primeTimer !== null) {
            clearTimeout(this.primeTimer);
            this.primeTimer = null;
        }
        const primed = this.primedFor(posFrames);
        this.playing = true;
        if (primed && at === null) this.startPrimed(primed);
        else {
            this.unprime();
            let frame = this.opFrame(true);
            if (at !== null) frame = Math.max(frame, this.gridFrame(at - this.latencyOf(this.wantedRoute()) / this.sampleRate));
            this.start(frame, posFrames);
        }
        this.setTicking(true);
        this.tick();
        this.emit('play');
    }

    /**
     * The context time a song position will be heard at, as playing now goes on (before any loop wrap), or null at
     * rest. For sounds made outside the player that must meet the music — a count-in.
     */
    heardAt(position: number): number | null {
        if (!this.playing) return null;
        const e = [...this.timeline.entries].reverse().find(x => x.kind === 'play');
        return e && e.rate > 0 ? e.t + (position - e.pos) / e.rate : null;
    }

    /** The context time the sound of a start decided now would be heard at, at the soonest. */
    soonestStart(): number {
        return (this.gridFrame(this.contextNow() + LEAD) + this.latencyOf(this.wantedRoute())) / this.sampleRate;
    }

    pause(): void {
        if (!this.playing) return;
        const frame = this.opFrame(true);
        this.cut(frame, Math.min(this.duration, this.timeline.positionAt(frame / this.sampleRate)), 'pause');
        this.playing = false;
        this.schedulePrime();
        this.armIdle();
        this.emit('pause');
    }

    /** Where play() would start from, in frames: the resting place, or the start (the loop's) when resting at the end. */
    private startFrames(): number {
        let pos = this.timeline.last.pos;
        const loop = this.currentLoop;
        if (pos >= this.duration - 1e-6) pos = loop ? loop.start : 0;
        else if (loop?.pause && pos >= loop.end - 1e-6) pos = loop.start;
        return Math.round(pos * this.sampleRate);
    }

    /**
     * Moves to a position in seconds. Playing goes on from there. It lands on the sample at `position`, or on the next
     * one — never before the time asked: seeking to the start of a chord or a beat lands in it.
     */
    seek(position: number): void {
        if (!Number.isFinite(position)) throw new RangeError(`seek position must be a number, got ${position}`);
        if (!this.track) throw new PlayerError('no-song', 'nothing is loaded');
        const lastFrame = Math.round(this.duration * this.sampleRate);
        const posFrames = Math.max(0, Math.min(lastFrame, Math.ceil(position * this.sampleRate - 1e-6)));
        const pos = posFrames / this.sampleRate;
        if (!this.playing) {
            this.rest(pos, 'seek');
            this.schedulePrime();
        } else {
            const frame = this.opFrame(true);
            this.cut(frame, pos, 'seek');
            if (pos >= this.duration) {
                this.playing = false;
                this.emit('seek');
                this.emit('ended');
                return;
            }
            this.start(frame + FADE, posFrames);
            this.sent = { until: this.timeline.last.t, pos };
            this.tick();
        }
        this.emit('seek');
    }

    /** The song position leaving the speakers now, seconds: read it every animation frame. */
    get position(): number {
        return this.positionAt(this.outputTime());
    }

    /** Where the transport is, at once: after seek() the place sought, after pause() the place it rests at. */
    get transportPosition(): number {
        if (!this.playing) return Math.min(this.duration, this.timeline.last.pos);
        const now = this.context.currentTime;
        if (this.sent !== null && now < this.sent.until) return this.sent.pos;
        return this.positionAt(now);
    }

    positionAt(contextTime: number): number {
        return Math.min(this.duration, this.timeline.positionAt(contextTime));
    }

    get isPlaying(): boolean {
        return this.playing;
    }

    get loop(): { start: number; end: number } | null {
        return this.currentLoop ? { start: this.currentLoop.start, end: this.currentLoop.end } : null;
    }

    /**
     * Sets the loop: reaching `end` goes on from `start`, cross-faded, without a gap. `pause`: reaching `end` stops
     * there instead, as the song's end does ('ended'); play() then goes on from `start` (a count-in is put between).
     */
    setLoop(start: number, end: number, pause = false): void {
        if (!this.track) throw new PlayerError('no-song', 'nothing is loaded');
        if (!(start >= 0 && end <= this.duration + 1e-9 && end - start >= MIN_LOOP_SECONDS))
            throw new RangeError(`loop ${start}…${end} must lie inside 0…${this.duration} and be at least ${MIN_LOOP_SECONDS} s long`);
        const rate = this.sampleRate;
        this.currentLoop = { start: Math.round(start * rate) / rate, end: Math.round(Math.min(end, this.duration) * rate) / rate, pause };
        this.replan();
        this.emit('change');
    }

    clearLoop(): void {
        if (!this.currentLoop) return;
        this.currentLoop = null;
        this.replan();
        this.emit('change');
    }

    // ---- tempo ----------------------------------------------------------------------------------------

    /** Speed, 0.5…1.5; the pitch does not change. */
    get tempo(): number {
        return this.currentTempo;
    }

    set tempo(value: number) {
        if (!(value >= MIN_TEMPO && value <= MAX_TEMPO)) throw new RangeError(`tempo must be ${MIN_TEMPO}…${MAX_TEMPO}, got ${value}`);
        if (value === this.currentTempo) return;
        this.currentTempo = value;
        this.applyTempo();
        this.emit('change');
    }

    // ---- mixer ----------------------------------------------------------------------------------------

    /** The mixer channel of the recording ('track'), of an instrument (its name) or of a lane (its name). */
    channel(name: string): ChannelStrip {
        const strip = this.graph.strips.get(name);
        if (!strip) throw new RangeError(`no channel "${name}"`);
        return strip;
    }

    hasChannel(name: string): boolean {
        return this.graph.strips.has(name);
    }

    // ---- scheduled sounds -----------------------------------------------------------------------------

    addLane(name: string): Lane {
        const lane = new Lane(this.context, name, this.graph.addStrip(name, 'lane'));
        this.lanes.set(name, lane);
        this.graph.updateAudibility();
        return lane;
    }

    lane(name: string): Lane {
        const lane = this.lanes.get(name);
        if (!lane) throw new RangeError(`no lane "${name}"`);
        return lane;
    }

    // ---- lifecycle ------------------------------------------------------------------------------------

    /** Stops everything and closes the AudioContext. */
    async dispose(): Promise<void> {
        if (this.disposed) return;
        await this.unload();
        this.disposed = true;
        this.disarmIdle();
        if (this.primeTimer !== null) clearTimeout(this.primeTimer);
        this.setTicking(false);
        this.ticker.terminate();
        for (const lane of this.lanes.values()) lane.dispose();
        this.lanes.clear();
        this.context.removeEventListener('statechange', this.onStateChange);
        disposeShifter(this.shifter);
        this.graph.dispose();
        await this.context.close();
    }

    // ==================================================================================================
    // inside
    // ==================================================================================================

    private emit(type: PlayerEvent, detail: unknown = null): void {
        this.dispatchEvent(new CustomEvent(type, { detail }));
    }

    /** Problems that happen away from a call (in the scheduler, in the audio thread) arrive as 'error' events. */
    private raise(error: Error): void {
        this.dispatchEvent(new CustomEvent('error', { detail: error }));
    }

    /**
     * Gets the AudioContext running. A browser that has not yet seen a tap or a key on the page leaves resume() pending
     * for ever, so that case is given a second and then named. Once the page has had one the output is merely slow to
     * open at times; it is waited for.
     */
    private async resume(): Promise<void> {
        const context = this.context;
        this.suspendedForIdleness = false;
        if (context.state === 'running') return;
        const hadGesture = Boolean(navigator.userActivation?.hasBeenActive);
        const resumed = context.resume();
        let timer: ReturnType<typeof setTimeout> | undefined;
        const waited = new Promise<void>(resolve => { timer = setTimeout(resolve, hadGesture ? OUTPUT_START_MS : NO_GESTURE_MS); });
        try {
            await Promise.race([resumed, waited]);
        } finally {
            clearTimeout(timer);
        }
        if ((context.state as AudioContextState) !== 'running') {
            resumed.then(null, (error: Error) => this.raise(error));
            if (hadGesture) throw new PlayerError('no-output', `the audio output did not start within ${OUTPUT_START_MS / 1000} s`);
            throw new PlayerError('gesture-required', 'the browser has not allowed sound yet');
        }
    }

    /** The way out the music should take: untouched at tempo 1, else through the stretcher. */
    private wantedRoute(): Route {
        return this.currentTempo !== 1 ? 'stretch' : 'dry';
    }

    private latencyOf(route: Route): number {
        return route === 'dry' ? 0 : this.shifter.latencyFrames;
    }

    /** Opens one of a deck's ways out and closes the other, from just before `frame`. */
    private setSends(deck: number, route: Route, frame: number): void {
        const at = this.justBefore(frame);
        const sends = this.graph.decks[deck]!.sends;
        sends.dry.gain.setValueAtTime(route === 'dry' ? 1 : 0, at);
        sends.stretch.gain.setValueAtTime(route === 'stretch' ? 1 : 0, at);
    }

    /** Makes the stretcher empty, running and at the present ratio, from `frame`. `open`: whether its output is let through. */
    private readyShifter(frame: number, open: boolean): void {
        const shifter = this.shifter, at = this.justBefore(frame);
        shifter.run.cancelScheduledValues(at);          // a feeding ahead that was dropped may have left "stand still" for later
        shifter.epoch.setValueAtTime(++this.epoch, at);
        shifter.ratio.setValueAtTime(shifterRatio(this.currentTempo, 0), at);
        shifter.run.setValueAtTime(1, at);
        // (set on the frame itself, not just before it: a cut may be fading this gate out up to this very frame)
        this.graph.gate.gain.setValueAtTime(open ? 1 : 0, frame / this.sampleRate);
    }

    /** First block boundary at or after context time `t`, in frames. */
    private gridFrame(t: number): number {
        return Math.ceil(t * this.sampleRate / QUANTUM - 1e-6) * QUANTUM;
    }

    /** A time that falls just inside the block before `frame`: automation set there is in force from `frame`. */
    private justBefore(frame: number): number {
        return (frame - 0.5) / this.sampleRate;
    }

    /** The context time of the sound leaving the speakers now, running evenly between the clock's steps. */
    private outputTime(): number {
        const context = this.context;
        let t = context.currentTime;
        if (context.state === 'running') {
            const stamp = context.getOutputTimestamp();
            if (stamp.performanceTime && stamp.performanceTime > 0 && stamp.contextTime !== undefined)
                t = Math.min(t, stamp.contextTime + (performance.now() - stamp.performanceTime) / 1000);
        }
        if (t < this.lastOutputTime) t = this.lastOutputTime;
        this.lastOutputTime = t;
        return t;
    }

    /**
     * The context time now, never too early: for deciding that a moment is still ahead. `currentTime` can stand still
     * for a whole script task; the best recent reading, carried forward by the wall clock, does not.
     */
    private contextNow(): number {
        const context = this.context, time = context.currentTime;
        if (context.state !== 'running') {
            this.anchor = null;
            return time;
        }
        const wall = performance.now() / 1000, offset = time - wall;
        if (this.anchor === null || wall - this.anchor.wall > ANCHOR_SECONDS || offset >= this.anchor.offset) this.anchor = { offset, wall };
        return Math.max(time, wall + this.anchor.offset);
    }

    /** Whether sources scheduled for `frame` will all start on it: they do while the frame is still ahead. */
    private inTime(frame: number): boolean {
        return this.contextNow() + SAFE < frame / this.sampleRate;
    }

    /** The frame at which a change decided now can begin: far enough ahead, and not inside another change. */
    private opFrame(cut = false): number {
        const soon = this.gridFrame(this.contextNow() + LEAD);
        let frame = Math.max(soon, this.busyUntil, cut ? 0 : this.holdUntil);
        for (const e of this.timeline.entries) {
            if (e.wrap && e.wrap.xStart <= frame && frame < e.wrap.xEnd) frame = Math.ceil(e.wrap.xEnd / QUANTUM) * QUANTUM;
        }
        this.busyUntil = Math.max(this.busyUntil, Math.min(frame, soon));
        return frame;
    }

    /** Resolves once every fade begun so far has run out. */
    private async settled(): Promise<void> {
        while (this.context.state === 'running' && this.context.currentTime * this.sampleRate < this.busyUntil + QUANTUM)
            await new Promise(resolve => setTimeout(resolve, 10));
    }

    /** Puts the timeline at rest at `pos` (not playing), from what the speakers give now on. */
    private rest(pos: number, kind: EntryKind): void {
        const t = Math.max(this.outputTime(), this.timeline.last.t);
        this.timeline.append({ t, pos, rate: 0, cut: true, kind });
    }

    // ---- groups of sources ----------------------------------------------------------------------------

    /**
     * Schedules the sources of the set in use to start together at `frame`. The group is silent until openGroup() fades
     * it in — done only once it is certain the start was met, so a group that has to be dropped is never heard.
     */
    private startGroup(frame: number, posFrames: number, rate: number, deck: number): SourceGroup {
        const context = this.context, sr = this.sampleRate;
        if (rate === 1) posFrames = Math.round(posFrames);       // at tempo 1 positions are whole frames; keep them so
        const when = frame / sr, offset = Math.max(0, posFrames) / sr;
        const group: SourceGroup = { sources: [], gains: [], deck, startFrame: frame, endFrame: null, fadeFrame: null, left: 0 };
        for (const { name, buffer } of this.sources()) {
            const source = context.createBufferSource();
            source.buffer = buffer;
            source.playbackRate.value = rate;
            const gain = context.createGain();
            gain.gain.value = 0;
            source.connect(gain);
            gain.connect(this.graph.strips.get(name)!.inputOf(deck));
            source.addEventListener('ended', () => { if (--group.left === 0) this.killGroup(group); }, { once: true });
            group.left++;
            source.start(when, offset);
            group.sources.push(source);
            group.gains.push(gain);
        }
        this.groups.push(group);
        return group;
    }

    /** Fades a started group in between two frames (both still ahead). */
    private openGroup(group: SourceGroup, fromFrame: number, toFrame: number): void {
        const sr = this.sampleRate;
        for (const gain of group.gains) {
            if (toFrame === fromFrame) {            // at once: the stream goes on from what was fed before
                gain.gain.setValueAtTime(1, fromFrame / sr);
                continue;
            }
            gain.gain.setValueAtTime(0, fromFrame / sr);
            gain.gain.linearRampToValueAtTime(1, toFrame / sr);
        }
    }

    private fadeOut(group: SourceGroup, frame: number, frames: number): void {
        const sr = this.sampleRate;
        for (const gain of group.gains) {
            gain.gain.setValueAtTime(1, frame / sr);
            gain.gain.linearRampToValueAtTime(0, (frame + frames) / sr);
        }
        group.fadeFrame = frame;
        group.endFrame = frame + frames;
    }

    private killGroup(group: SourceGroup): void {
        const i = this.groups.indexOf(group);
        if (i < 0) return;
        this.groups.splice(i, 1);
        for (const source of group.sources) {
            source.stop();
            source.disconnect();
        }
        for (const gain of group.gains) gain.disconnect();
    }

    // ---- changes --------------------------------------------------------------------------------------

    /**
     * Begins playing from `posFrames`: the sources start at `frame` — or, if the page is held up past it, at the next
     * moment that can be met — a fade's length before the place, which itself sounds at full level.
     */
    private start(frame: number, posFrames: number): void {
        const sr = this.sampleRate, tempo = this.currentTempo;
        const route = this.wantedRoute(), deck = 1 - this.deck;
        const pre = this.preRoll(posFrames, tempo);
        for (let attempt = 1; ; attempt++) {
            this.setSends(deck, route, frame);
            if (route !== 'dry') this.readyShifter(frame, true);
            const group = this.startGroup(frame, posFrames - pre * tempo, tempo, deck);
            if (this.inTime(frame)) {
                this.openGroup(group, frame, frame + pre);
                break;
            }
            this.killGroup(group);
            if (attempt === START_ATTEMPTS) {
                this.playing = false;
                throw new PlayerError('busy', 'the page was too busy to start the sound on time');
            }
            frame = this.opFrame(true);
        }
        this.route = route;
        this.deck = deck;
        this.latencyFrames = this.latencyOf(route);
        this.timeline.append({ t: (frame + pre + this.latencyFrames) / sr, pos: posFrames / sr, rate: tempo, cut: true, kind: 'play' });
        this.busyUntil = frame + FADE;
    }

    /** How many frames before a place the sources start, so the fade-in is over when the place itself sounds. */
    private preRoll(posFrames: number, rate: number): number {
        return Math.min(FADE, Math.floor(posFrames / rate));
    }

    /** Begins playing through a stretcher that was fed ahead (prime()): what was fed is heard at once. */
    private startPrimed(primed: Primed): void {
        const sr = this.sampleRate, shifter = this.shifter;
        let frame = Math.max(this.opFrame(true), primed.readyFrame);
        for (let attempt = 1; ; attempt++) {
            const at = this.justBefore(frame);
            shifter.run.setValueAtTime(1, at);
            this.graph.gate.gain.setValueAtTime(1, at);
            const group = this.startGroup(frame, primed.nextFrames, primed.tempo, primed.deck);
            if (this.inTime(frame)) {
                this.openGroup(group, frame, frame);
                break;
            }
            this.killGroup(group);
            shifter.run.cancelScheduledValues(at);
            this.graph.gate.gain.cancelScheduledValues(at);
            if (attempt === START_ATTEMPTS) {
                this.playing = false;
                this.primed = null;
                throw new PlayerError('busy', 'the page was too busy to start the sound on time');
            }
            frame = this.opFrame(true);
        }
        this.primed = null;
        this.route = 'stretch';
        this.deck = primed.deck;
        this.latencyFrames = shifter.latencyFrames;
        this.timeline.append({ t: (frame + primed.pre) / sr, pos: primed.posFrames / sr, rate: primed.tempo, cut: true, kind: 'play' });
        this.busyUntil = frame + FADE;
    }

    /** Silences everything from `frame` (what is heard stops there) and leaves the timeline at rest at `pos`. */
    private cut(frame: number, pos: number, kind: EntryKind): void {
        const sr = this.sampleRate, t = frame / sr, end = (frame + FADE) / sr, graph = this.graph;
        this.truncate(t);
        // whatever was arranged for later is withdrawn
        for (const deck of graph.decks) for (const send of Object.values(deck.sends)) send.gain.cancelScheduledValues(t);
        for (const group of [...this.groups]) {
            if (group.startFrame >= frame) {
                this.killGroup(group);
                continue;
            }
            if (group.endFrame !== null && group.fadeFrame !== null && group.fadeFrame > frame) {
                for (const gain of group.gains) gain.gain.cancelScheduledValues(this.justBefore(group.fadeFrame));
                group.endFrame = null;
            }
            if (group.endFrame === null) this.fadeOut(group, frame, FADE);
        }
        // the stretcher still holds its delay's worth of the old sound: fade what comes out, then empty it
        const shifter = this.shifter, gate = graph.gate.gain;
        for (const param of [gate, shifter.ratio, shifter.run, shifter.epoch]) param.cancelScheduledValues(t);
        gate.setValueAtTime(1, t);
        gate.linearRampToValueAtTime(0, end);
        gate.setValueAtTime(1, end);
        shifter.epoch.setValueAtTime(++this.epoch, this.justBefore(frame + FADE));
        this.timeline.append({ t, pos, rate: 0, cut: true, kind });
        for (const lane of this.lanes.values()) lane.cutAt(t);
        this.busyUntil = frame + FADE;
        this.holdUntil = 0;
    }

    /** Withdraws everything planned to be heard after context time `t`. */
    private truncate(t: number): void {
        const removed = this.timeline.truncateAfter(t);
        for (const e of removed) {
            if (!e.wrap) continue;
            if (e.wrap.group) this.killGroup(e.wrap.group);
            for (const group of e.wrap.old) {
                for (const gain of group.gains) gain.gain.cancelScheduledValues(this.justBefore(e.wrap.xStart));
                group.endFrame = null;
            }
        }
        if (removed.length > 0) for (const lane of this.lanes.values()) lane.truncate(t, this.timeline);
    }

    /** The loop changed: what was committed under the old one is withdrawn; the scheduler plans again. */
    private replan(): void {
        if (!this.playing) return;
        this.truncate((this.opFrame() + this.latencyFrames) / this.sampleRate);
    }

    /** The instruments were switched while playing: the other set takes over at the same position, cross-faded. */
    private swapSources(): void {
        const sr = this.sampleRate;
        for (let attempt = 1; ; attempt++) {
            const frame = this.opFrame();
            const heard = (frame + this.latencyFrames) / sr;
            this.truncate(heard);
            const entry = this.timeline.entryAt(heard);
            if (entry.rate === 0) return;                          // the end is already committed
            const pos = this.timeline.positionAt(heard);
            if (pos >= this.duration) {                            // the set switched to is shorter and is already over
                this.cut(frame, this.duration, 'end');
                this.playing = false;
                this.emit('ended');
                return;
            }
            const old = this.groups.filter(g => g.endFrame === null);
            const group = this.startGroup(frame, pos * sr, entry.rate, this.deck);
            if (this.inTime(frame)) {
                this.openGroup(group, frame, frame + FADE);
                for (const g of old) this.fadeOut(g, frame, FADE);
                this.busyUntil = frame + FADE;
                return;
            }
            this.killGroup(group);
            if (attempt === START_ATTEMPTS) throw new PlayerError('busy', 'the page was too busy to switch the instruments on time');
        }
    }

    private applyTempo(): void {
        if (!this.playing) {
            this.schedulePrime();
            return;
        }
        const route = this.wantedRoute();
        if (route !== this.route) {
            this.changeRoute(route);
            return;
        }
        if (route === 'dry') return;
        const sr = this.sampleRate, frame = this.opFrame();
        const heard = (frame + this.latencyFrames) / sr;
        this.truncate(heard);
        if (this.timeline.entryAt(heard).rate === 0) return;   // the end is already committed before the change could be heard
        const at = this.justBefore(frame);
        for (const group of this.groups) {
            if (group.endFrame !== null && group.endFrame <= frame) continue;
            for (const source of group.sources) source.playbackRate.setValueAtTime(this.currentTempo, at);
        }
        this.shifter.ratio.setValueAtTime(shifterRatio(this.currentTempo, 0), at);
        this.timeline.append({ t: heard, pos: this.timeline.positionAt(heard), rate: this.currentTempo, cut: false, kind: 'change' });
        for (const lane of this.lanes.values()) lane.truncate(heard, this.timeline);
    }

    /**
     * Moves the playing music to the other way out without stopping it. The two ways delay the sound differently, so
     * the new stream starts on the other deck early enough to be heard at the moment the old one — stopped early
     * enough — runs out; there they cross-fade.
     */
    private changeRoute(route: Route): void {
        const sr = this.sampleRate, tempo = this.currentTempo;
        const oldLatency = this.latencyFrames, newLatency = this.latencyOf(route), deck = 1 - this.deck;
        for (let attempt = 1; ; attempt++) {
            const frame = this.opFrame();
            const cross = frame + Math.max(oldLatency, newLatency);       // the frame at which the change is heard
            const heard = cross / sr;
            this.truncate(heard);
            if (this.timeline.entryAt(heard).rate === 0) return;          // the end comes first
            const pos = this.timeline.positionAt(heard);
            if (pos >= this.duration) return;
            const newStart = cross - newLatency, oldStop = cross - oldLatency;
            this.setSends(deck, route, newStart);
            if (route !== 'dry') this.readyShifter(newStart, true);
            const old = this.groups.filter(g => g.endFrame === null);
            const group = this.startGroup(newStart, pos * sr, tempo, deck);
            if (this.inTime(newStart)) {
                this.openGroup(group, newStart, newStart + FADE);
                for (const g of old) this.fadeOut(g, oldStop, ROUTE_FADE);
                this.timeline.append({ t: heard, pos, rate: tempo, cut: false, kind: 'change' });
                for (const lane of this.lanes.values()) lane.truncate(heard, this.timeline);
                this.route = route;
                this.deck = deck;
                this.latencyFrames = newLatency;
                this.holdUntil = cross + ROUTE_FADE;
                return;
            }
            this.killGroup(group);
            if (attempt === START_ATTEMPTS) throw new PlayerError('busy', 'the page was too busy to change the tempo on time');
        }
    }

    // ---- ready for play() -----------------------------------------------------------------------------

    /** Something play() depends on changed while paused: what the stretcher was fed is dropped, and it is fed again shortly. */
    private schedulePrime(): void {
        if (this.playing || this.disposed) return;
        this.unprime();
        if (this.primeTimer !== null) clearTimeout(this.primeTimer);
        this.primeTimer = setTimeout(() => {
            this.primeTimer = null;
            this.prime();
        }, PRIME_DELAY_MS);
    }

    private unprime(): void {
        if (this.primed === null) return;
        this.killGroup(this.primed.group);
        this.primed = null;
    }

    /** What was fed ahead, if it is still what play() from `posFrames` needs. */
    private primedFor(posFrames: number): Primed | null {
        const p = this.primed;
        if (p === null || p.posFrames !== posFrames || this.wantedRoute() !== 'stretch' || p.tempo !== this.currentTempo) return null;
        return p;
    }

    /**
     * While paused with the stretcher wanted: plays it its delay's worth of the music from the resting place, silently,
     * and then holds it still. play() lets it run again and the music is heard at once instead of a delay later.
     */
    private prime(): void {
        if (this.playing || this.disposed || !this.track) return;
        if (this.wantedRoute() === 'dry') return;
        const context = this.context;
        if (context.state !== 'running') {
            // asleep for idleness: wake to get ready (the 'statechange' brings us back here); never started: play() does it
            if (this.suspendedForIdleness) {
                this.suspendedForIdleness = false;
                context.resume().then(null, (error: Error) => this.raise(error));
            }
            return;
        }
        this.disarmIdle();
        const sr = this.sampleRate, shifter = this.shifter, latency = shifter.latencyFrames;
        const posFrames = this.startFrames(), deck = 1 - this.deck, tempo = this.currentTempo;
        // it runs for a whole number of blocks; the music starts inside the first so that exactly its delay's worth goes in
        const blocks = Math.ceil(latency / QUANTUM) * QUANTUM;
        const from = this.opFrame(true), until = from + blocks, musicFrom = until - latency;
        this.setSends(deck, 'stretch', from);
        this.readyShifter(from, false);
        shifter.run.setValueAtTime(0, this.justBefore(until));
        const pre = this.preRoll(posFrames, tempo);
        const group = this.startGroup(musicFrom, posFrames - pre * tempo, tempo, deck);
        if (!this.inTime(from)) {
            this.killGroup(group);
            this.schedulePrime();
            return;
        }
        this.openGroup(group, musicFrom, musicFrom + pre);
        for (const source of group.sources) source.stop(until / sr);
        group.endFrame = until;
        this.primed = { deck, posFrames, tempo, group, pre, readyFrame: until, nextFrames: posFrames + (latency - pre) * tempo };
        this.setTicking(true);
        this.armIdle();
    }

    // ---- letting the output go ------------------------------------------------------------------------

    private armIdle(): void {
        this.disarmIdle();
        if (this.playing || this.disposed) return;
        this.idleTimer = setTimeout(() => {
            this.idleTimer = null;
            if (this.playing || this.context.state !== 'running') return;
            const busy = this.groups.length > 0 || [...this.lanes.values()].some(lane => lane.voices.size > 0) || this.primeTimer !== null;
            if (busy) {
                this.armIdle();
                return;
            }
            this.suspendedForIdleness = true;
            this.context.suspend().then(null, (error: Error) => this.raise(error));
        }, IDLE_SUSPEND_SECONDS * 1000);
    }

    private disarmIdle(): void {
        if (this.idleTimer !== null) {
            clearTimeout(this.idleTimer);
            this.idleTimer = null;
        }
    }

    // ---- the scheduler --------------------------------------------------------------------------------

    private setTicking(on: boolean): void {
        if (on === this.ticking) return;
        this.ticking = on;
        this.ticker.postMessage(on ? TICK_MS : 0);
    }

    private tick(): void {
        if (this.disposed) return;
        const now = this.contextNow(), rendered = this.context.currentTime, sr = this.sampleRate;
        if (this.playing) this.plan(now);

        for (const e of this.timeline.entries) {
            if (e.announced || e.t > rendered) continue;
            e.announced = true;
            if (e.kind === 'loop') this.emit('loop', { time: e.t, position: e.pos });
            if (e.kind === 'end' && this.playing) {
                this.playing = false;
                this.schedulePrime();
                this.armIdle();
                this.emit('ended');
            }
        }

        let voices = 0;
        for (const lane of this.lanes.values()) {
            try {
                lane.schedule(now, now + LANE_AHEAD, this.timeline);
            } catch (error) {
                lane.setProvider(null);             // a provider that throws would throw on every tick
                this.raise(error as Error);
            }
            lane.sweep(rendered);
            voices += lane.voices.size;
        }
        for (const group of [...this.groups]) {
            if (group.endFrame !== null && rendered * sr > group.endFrame + QUANTUM) this.killGroup(group);
        }
        this.timeline.prune(rendered - 1);
        if (!this.playing && this.groups.length === 0 && voices === 0) this.setTicking(false);
    }

    /** Commits the loop wrap, the loop's stop or the end of the song once it is near. */
    private plan(now: number): void {
        const sr = this.sampleRate;
        for (let i = 0; i < 16; i++) {
            const last = this.timeline.last;
            if (last.rate === 0) return;
            const loop = this.currentLoop, duration = this.duration;
            const inLoop = loop !== null && last.pos < loop.end - 1e-9 && loop.end <= duration;
            const wraps = inLoop && !loop.pause;
            const target = inLoop ? loop.end : duration;
            const heard = last.t + Math.max(0, target - last.pos) / last.rate;
            if (heard - this.latencyFrames / sr - LOOP_CROSSFADE / 2 > now + PLAN_AHEAD) return;
            if (wraps) {
                if (!this.commitWrap(heard, now)) return;            // the page was held up; the next tick tries again
            } else {
                const end: Entry = { t: heard, pos: target, rate: 0, cut: true, kind: 'end' };
                // the song (or a pausing loop) ends faded, like every stop
                const stop = Math.round(heard * sr) - this.latencyFrames, old = this.groups.filter(g => g.endFrame === null);
                for (const group of old) this.fadeOut(group, stop - FADE, FADE);
                end.wrap = { group: null, old, xStart: stop - FADE, xEnd: stop };
                this.timeline.append(end);
                for (const lane of this.lanes.values()) lane.cutAt(heard);
                return;
            }
        }
    }

    private commitWrap(heard: number, now: number): boolean {
        const sr = this.sampleRate, last = this.timeline.last, rate = last.rate, loop = this.currentLoop!;
        const half = Math.round(LOOP_CROSSFADE * sr / 2);
        // if the scheduler was held up past the moment, the wrap happens as soon as it can
        const wrapFrame = Math.max(Math.round(heard * sr) - this.latencyFrames, this.gridFrame(now + LEAD) + half, this.busyUntil + half);
        const startFrames = Math.round(loop.start * sr);
        const lead = Math.min(half, Math.floor(startFrames / rate));       // frames of lead-in before the loop's start
        const old = this.groups.filter(g => g.endFrame === null);
        const group = this.startGroup(wrapFrame - lead, startFrames - lead * rate, rate, this.deck);
        if (!this.inTime(wrapFrame - half)) {
            this.killGroup(group);
            return false;
        }
        this.openGroup(group, wrapFrame - lead, wrapFrame + half);
        for (const g of old) this.fadeOut(g, wrapFrame - half, 2 * half);
        const t = (wrapFrame + this.latencyFrames) / sr;
        this.timeline.append({ t, pos: startFrames / sr, rate, cut: true, kind: 'loop', wrap: { group, old, xStart: wrapFrame - half, xEnd: wrapFrame + half } });
        for (const lane of this.lanes.values()) lane.cutAt(t);
        return true;
    }
}