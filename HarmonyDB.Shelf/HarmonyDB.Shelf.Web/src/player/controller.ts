// The player of the page: one for the app, outside React, so that the music goes on while the page scrolls and while
// another view of an analysis opens. It plays one source at a time:
//
//   - an analysis: its recording plays at once, streamed by the browser's audio element; meanwhile it is decoded, and
//     the sound (sound/player.ts) takes over from the same place without a stop — from then on the loop is seamless and
//     the metronome and the chords are on the music's own clock. Its separate instruments, asked for, are fetched and
//     decoded (the recording plays meanwhile) and take over locked to the sample; turned off, they are let go;
//   - a track of a music service: streamed by the audio element (Range requests), its clip by a video element in step
//     with it.
//
// Media addresses expire (data/media.ts): they are asked for anew before they are used after that, and a stream that
// fails on an old address goes on from the same place with a new one.
//
//   player.open(source)            the source of the page (the same source again changes nothing)
//   player.setPiece(piece)         an analysis's piece: beats for the metronome, chords for their sound, sections
//   player.play() / pause() / toggle() / seek(t) / playFrom(t)
//   player.toggleLoop() / setLoop(a, b) / clearLoop()
//   player.setTempo(percent) / setTrainer(on) / setByStems(on) / setRow(name, { on, volume }) / setSolo(name)
//   player.setCountIn(on) / setClip(on)
//   player.position()              seconds, what is heard now — read it every animation frame (useClock)
//   usePlayer()                    the state, for React

import { useEffect, useSyncExternalStore } from 'react';
import { ApiError } from '../api/client';
import { errorText } from '../api/errors';
import { chordsMedia, trackMedia, type MediaAddress, type MediaStem, type TrackRoute } from '../data/media';
import type { MusicTrack } from '../data/music';
import { i18n } from '../i18n';
import { toast } from '../app/toasts';
import { barAt, sectionAt, type Piece } from '../analysis/piece';
import { pluckChord } from '../chords/sound';
import type { Player } from './sound/player';
import { PlayerError, MIN_LOOP_SECONDS, type Stem } from './sound/types';
import TickWorker from './sound/tickWorker.ts?worker';
import { Peaks } from './peaks';
import { playClick } from './sounds';

export type PlayerSource =
    // share: opened by a share link that carries its sharer's access — the media are asked through it
    | { kind: 'analysis'; chordsId: string; share?: string | null; title: string; subtitle: string }
    // route: its addresses asked bound to the song that lists it (the public page); none — as the app asks them
    | { kind: 'track'; track: MusicTrack; route?: TrackRoute | null };

export interface MixRow { on: boolean; volume: number }

/** The rows of the mixer: the recording, its instruments, the metronome, the chords' sound. */
export type RowName = 'recording' | MediaStem | 'metronome' | 'chords';

export const stemOrder: MediaStem[] = ['vocals', 'guitar', 'bass', 'drums', 'piano', 'other'];

export interface PlayerState {
    source: PlayerSource | null;
    status: 'idle' | 'loading' | 'ready' | 'failed';
    error: string | null;
    playing: boolean;
    duration: number;
    loop: { start: number; end: number } | null;
    /** percent, 50…150 */
    tempo: number;
    trainer: boolean;
    /** the separate instruments the source has */
    stems: MediaStem[];
    byStems: boolean;
    /** while the instruments are being made ready: how many are */
    stemsLoading: { done: number; total: number } | null;
    stemsReady: boolean;
    mix: Record<RowName, MixRow>;
    solo: MediaStem | null;
    /** the analysis has beats and chords: the metronome, the chords' sound and the count-in can be */
    hasBeats: boolean;
    countIn: boolean;
    /** a bar of clicks is being counted before the loop */
    counting: boolean;
    hasVideo: boolean;
    clip: boolean;
    /** the waveform of the analysis's recording is there (peaks) */
    wave: boolean;
}

const row = (on = true, volume = 1): MixRow => ({ on, volume });

const initial: PlayerState = {
    source: null, status: 'idle', error: null, playing: false, duration: 0, loop: null, tempo: 100, trainer: false,
    stems: [], byStems: false, stemsLoading: null, stemsReady: false,
    mix: {
        recording: row(), vocals: row(), guitar: row(), bass: row(), drums: row(), piano: row(), other: row(),
        metronome: row(false, 0.8), chords: row(false, 0.8),
    },
    solo: null, hasBeats: false, countIn: false, counting: false, hasVideo: false, clip: false, wave: false,
};

const sourceKey = (source: PlayerSource) => (source.kind === 'analysis' ? `a:${source.chordsId}:${source.share ?? ''}` : `t:${source.track.service}:${source.track.id}`);

/** An address is asked for anew this long before it expires. */
const EXPIRY_MARGIN_MS = 60_000;
/** How often the streams are looked after while they play: the loop, the instruments' and the clip's step. */
const WATCH_MS = 25;
/** A clip further than this from the sound is put back in step. */
const CLIP_DRIFT = 0.15;
/** The recording's stream hands over to the decoded sound this far ahead, seconds. */
const HANDOVER_LEAD = 0.15;
/** A loop made by the button where there is nothing to go by: from a little before the place, this long. */
const PLAIN_LOOP_BEFORE = 2, PLAIN_LOOP_LENGTH = 12;

const t = i18n.t.bind(i18n);

/** A streamed audio element at a tempo, its pitch kept. */
function streamElement(url: string, tempo: number): HTMLAudioElement {
    const audio = new Audio();
    audio.preload = 'auto';
    audio.preservesPitch = true;
    audio.src = url;
    audio.playbackRate = tempo / 100;
    return audio;
}

function release(element: HTMLMediaElement): void {
    element.pause();
    element.removeAttribute('src');
    element.load();
}

class PlayerController {
    private state: PlayerState = initial;
    private readonly listeners = new Set<() => void>();
    private key: string | null = null;
    private generation = 0;
    private piece: Piece | null = null;

    // the analysis: the sound (one AudioContext for the page, made at the first analysis), its media, its instruments
    private sound: Player | null = null;
    private soundPromise: Promise<Player> | null = null;
    private expiresAt = 0;
    private stemUrls: { stem: MediaStem; url: string }[] = [];
    /** the analysis's recording decoded and handed to the sound (the instruments wait for it) */
    private decoded: Promise<void> | null = null;
    /** counts the askings for the instruments: a load asked before the last one is dropped */
    private stemsAsked = 0;
    /** the waveform of the analysis's recording */
    peaks: Peaks | null = null;

    // the stream (a track; an analysis's recording until it is decoded), the clip's video element
    private audio: HTMLAudioElement | null = null;
    private video: HTMLVideoElement | null = null;
    private videoExpiresAt = 0;
    /** a failed stream, or clip, is tried once more with a new address; failing again, it is given up */
    private streamRetried = false;
    /** a recording chosen with a tap: it plays once it is open (playChosen) */
    private wantPlay = false;
    /** and where it starts (seconds; 0: the beginning) */
    private wantFrom = 0;
    private clipRetried = false;
    private watch: Worker | null = null;

    // the trainer counts the passes of the loop; the count-in's clicks and the moment they are over
    private passes = 0;
    private countEnd: ConstantSourceNode | null = null;
    private countSources: AudioScheduledSourceNode[] = [];

    // ---- for React ------------------------------------------------------------------------------------

    readonly subscribe = (listener: () => void) => {
        this.listeners.add(listener);
        return () => this.listeners.delete(listener);
    };

    readonly getState = () => this.state;

    private set(patch: Partial<PlayerState>): void {
        this.state = { ...this.state, ...patch };
        for (const listener of this.listeners) listener();
        if (patch.status === 'failed') this.wantPlay = false;
        if (patch.status === 'ready' && this.wantPlay) {
            this.wantPlay = false;
            const from = this.wantFrom;
            this.wantFrom = 0;
            if (from > 0) this.seek(from);
            void this.play();
        }
    }

    // ---- the source -----------------------------------------------------------------------------------

    /** Opens the page's source; the same source again changes nothing (the music goes on). */
    open(source: PlayerSource): void {
        const key = sourceKey(source);
        if (key === this.key) {
            this.set({ source });
            return;
        }
        this.close();
        this.key = key;
        const generation = ++this.generation;
        // a YouTube track has its picture (a clip, or its cover over the sound) unless the service says it is not a video
        const hasVideo = source.kind === 'track' && source.track.service === 'youtube' && source.track.isVideo !== false;
        this.streamRetried = false;
        this.clipRetried = false;
        this.set({ ...initial, mix: this.state.mix, tempo: this.state.tempo, countIn: this.state.countIn, source, status: 'loading', hasVideo });
        const opened = source.kind === 'analysis' ? this.openAnalysis(source.chordsId, source.share ?? null, generation) : this.openTrack(source.track, source.route ?? null, generation);
        opened.catch(error => {
            if (generation !== this.generation) return;
            this.set({ status: 'failed', error: errorText(error) });
        });
    }

    /** The source leaves the page: the music stops, what was held is let go. */
    close(source?: PlayerSource): void {
        if (source && sourceKey(source) !== this.key) return;
        this.generation++;
        this.key = null;
        this.piece = null;
        this.stopCount();
        this.stopWatch();
        if (this.sound) void this.sound.unload();
        this.peaks = null;
        this.stemUrls = [];
        this.decoded = null;
        if (this.audio) release(this.audio);
        this.audio = null;
        if (this.video) release(this.video);
        this.video = null;
        this.set({ ...initial, mix: this.state.mix, tempo: this.state.tempo, countIn: this.state.countIn });
    }

    /** The analysis's piece: the metronome's beats, the chords' sound, the sections the loop button goes by. */
    setPiece(piece: Piece | null): void {
        this.piece = piece;
        this.set({ hasBeats: !!piece && piece.beats.length > 0 });
        if (this.sound) {
            this.sound.lane('metronome').invalidate();
            this.sound.lane('chords').invalidate();
        }
    }

    private async getSound(): Promise<Player> {
        if (this.sound) return this.sound;
        // (the sound, with its stretcher, is loaded the first time a recording is opened)
        this.soundPromise ??= import('./sound/player').then(({ Player }) => startInTime(Player.create())).then(sound => {
            const metronome = sound.addLane('metronome');
            metronome.setProvider((from, to) => this.clicks(from, to));
            const chords = sound.addLane('chords');
            chords.setProvider((from, to) => this.chordSounds(from, to));
            sound.addEventListener('play', () => this.set({ playing: true }));
            sound.addEventListener('pause', () => this.stopped());
            sound.addEventListener('ended', () => this.ended(sound));
            sound.addEventListener('loop', () => this.passed());
            sound.addEventListener('error', event => toast(playerErrorText((event as CustomEvent<Error>).detail)));
            this.sound = sound;
            this.applyMix();
            return sound;
        }, error => {
            // (asked again next time: the next song may get it)
            this.soundPromise = null;
            throw error;
        });
        return this.soundPromise;
    }

    private stopped(): void {
        this.set({ playing: false });
    }

    /** The sound reached the song's end — or, with the count-in, the loop's end: a bar of clicks, and the loop again. */
    private ended(sound: Player): void {
        const loop = this.state.loop;
        if (loop && this.countsIn() && sound.transportPosition >= loop.end - 1e-3) {
            this.passed();
            void this.countAndPlay(sound).catch(error => {
                this.stopped();
                toast(playerErrorText(error));
            });
            return;
        }
        this.stopped();
    }

    /** Whether the loop is played with a bar of clicks before it (its end then stops the sound: sound.setLoop's pause). */
    private countsIn(): boolean {
        return this.state.countIn && this.state.hasBeats;
    }

    /** The analysis: its recording streams at once; decoded, it is handed over to the sound. */
    private async openAnalysis(chordsId: string, share: string | null, generation: number): Promise<void> {
        const media = await chordsMedia(chordsId, share);
        if (generation !== this.generation) return;
        if (!media.recordingUrl) throw new ApiError(404, 'no_media');
        this.expiresAt = Date.parse(media.expiresAt);
        this.stemUrls = stemOrder.flatMap(stem => media.stems.filter(s => s.stem === stem).map(s => ({ stem, url: s.url })));
        this.set({ stems: this.stemUrls.map(s => s.stem), duration: media.durationSeconds ?? 0 });
        this.stream(media.recordingUrl, generation);
        this.decoded = this.decode(media.recordingUrl, generation);
        try {
            await this.decoded;
        } catch (error) {
            // the stream plays on: what needs the sound (a seamless loop, the metronome, the instruments) says it failed
            if (generation === this.generation) toast(playerErrorText(error));
        }
    }

    /** The recording decoded, given to the sound, and handed over to from the stream. */
    private async decode(url: string, generation: number): Promise<void> {
        const [sound, bytes] = await Promise.all([this.getSound(), fetchBytes(url)]);
        const buffer = await sound.decode(bytes);
        if (generation !== this.generation) return;
        await sound.load(buffer);
        if (generation !== this.generation) return;
        sound.tempo = this.state.tempo / 100;
        this.peaks = new Peaks(buffer);
        this.set({ wave: true });
        this.applyMix();
        await this.handOver(sound);
    }

    private async openTrack(track: MusicTrack, route: TrackRoute | null, generation: number): Promise<void> {
        const address = await trackMedia(track.service, track.id, 'audio', route);
        if (generation !== this.generation) return;
        this.expiresAt = Date.parse(address.expiresAt);
        this.stream(address.url, generation);
        this.set({ duration: (track.durationMs ?? 0) / 1000 });
    }

    /** The stream of the source's sound. */
    private stream(url: string, generation: number): void {
        const audio = streamElement(url, this.state.tempo);
        this.audio = audio;
        const mine = () => this.audio === audio;
        audio.addEventListener('loadedmetadata', () => mine() && this.set({ status: 'ready', duration: audio.duration }));
        audio.addEventListener('play', () => {
            if (!mine()) return;
            this.set({ playing: true });
            this.startWatch();
        });
        audio.addEventListener('pause', () => {
            if (!mine()) return;
            this.stopped();
            this.stopWatch();
            this.video?.pause();
        });
        audio.addEventListener('playing', () => { if (mine()) this.streamRetried = false; });
        audio.addEventListener('ended', () => mine() && this.stopped());
        audio.addEventListener('error', () => mine() && void this.recover(audio, generation));
        this.applyMix();
    }

    /**
     * The decoded recording takes over from the stream: at a moment a little ahead the sound starts from where the
     * stream will be then, and the stream stops there.
     */
    private async handOver(sound: Player): Promise<void> {
        const audio = this.audio;
        if (!audio) return;
        const rate = this.state.tempo / 100, loop = this.state.loop;
        this.audio = null;
        if (audio.paused) {
            sound.seek(audio.currentTime);
            release(audio);
        } else {
            await sound.context.resume();
            const at = sound.soonestStart() + HANDOVER_LEAD;
            sound.seek(audio.currentTime + (at - sound.context.currentTime) * rate);
            await sound.play(at);
            setTimeout(() => release(audio), Math.max(0, (at - sound.context.currentTime) * 1000));
        }
        this.set({ status: 'ready', duration: sound.duration });
        if (loop) this.setLoop(loop.start, loop.end);
        this.stopWatch();
    }

    /** The source's media anew: the stream's address, the instruments' addresses. */
    private async freshMedia(): Promise<string | null> {
        const source = this.state.source;
        if (source?.kind === 'track') {
            const address: MediaAddress = await trackMedia(source.track.service, source.track.id, 'audio', source.route);
            this.expiresAt = Date.parse(address.expiresAt);
            return address.url;
        }
        if (source?.kind === 'analysis') {
            const media = await chordsMedia(source.chordsId, source.share);
            this.expiresAt = Date.parse(media.expiresAt);
            this.stemUrls = stemOrder.flatMap(stem => media.stems.filter(s => s.stem === stem).map(s => ({ stem, url: s.url })));
            return media.recordingUrl ?? null;
        }
        return null;
    }

    /**
     * A stream failed: it is tried once more from the same place with a new address (an old one may have expired, a
     * connection dropped); failing again, the source says so, and play tries it all anew.
     */
    private async recover(element: HTMLAudioElement, generation: number): Promise<void> {
        if (generation !== this.generation) return;
        if (this.streamRetried) {
            this.set({ status: 'failed', error: errorText(new ApiError(0, 'unavailable')), playing: false });
            return;
        }
        this.streamRetried = true;
        const at = element.currentTime, wasPlaying = this.state.playing;
        try {
            const url = await this.freshMedia();
            if (generation !== this.generation || this.audio !== element) return;
            if (!url) throw new ApiError(404, 'no_media');
            element.src = url;
            element.currentTime = at;
            if (wasPlaying) await element.play();
        } catch (error) {
            if (generation !== this.generation) return;
            this.set({ status: 'failed', error: errorText(error), playing: false });
        }
    }

    /** Streams about to be used after their addresses expire get new ones first. */
    private async freshStreams(): Promise<void> {
        if (Date.now() < this.expiresAt - EXPIRY_MARGIN_MS || !this.audio) return;
        const url = await this.freshMedia();
        if (this.audio && url) {
            const at = this.audio.currentTime;
            this.audio.src = url;
            this.audio.currentTime = at;
        }
    }

    // ---- the transport --------------------------------------------------------------------------------

    /**
     * A recording chosen with a tap plays: the source opened next, as soon as it is ready (the tap lets sound begin),
     * from `from` seconds (the place it was left, when it is one coming back).
     */
    playChosen(from = 0): void {
        this.wantPlay = true;
        this.wantFrom = from;
    }

    /** Plays; called from a tap (the browser lets sound begin only then). */
    async play(): Promise<void> {
        const source = this.state.source;
        if (this.state.status === 'failed' && source) {
            // tried anew: the source opens again, and the next tap plays it
            this.key = null;
            this.open(source);
            return;
        }
        try {
            await this.freshStreams();
            if (this.audio) {
                if (this.state.loop && (this.audio.currentTime < this.state.loop.start || this.audio.currentTime >= this.state.loop.end)) this.audio.currentTime = this.state.loop.start;
                await this.audio.play();
                return;
            }
            const sound = this.sound;
            if (!sound?.loaded || this.state.status !== 'ready') return;
            const loop = this.state.loop;
            if (loop && this.state.countIn && this.state.hasBeats) {
                const at = sound.transportPosition;
                if (at < loop.start || at >= loop.end - 0.05) sound.seek(loop.start);
                await this.countAndPlay(sound);
                return;
            }
            await sound.play();
        } catch (error) {
            toast(playerErrorText(error));
        }
    }

    pause(): void {
        this.stopCount();
        this.audio?.pause();
        this.sound?.pause();
    }

    toggle(): void {
        if (this.state.playing || this.state.counting) this.pause();
        else void this.play();
    }

    /** Goes to a time; playing goes on from there. */
    seek(time: number): void {
        const at = Math.max(0, Math.min(time, this.state.duration || time));
        if (this.audio) {
            this.audio.currentTime = at;
            if (this.video && this.state.clip) this.video.currentTime = at;
            return;
        }
        if (this.sound?.loaded && this.state.status === 'ready') {
            this.stopCount();
            this.sound.seek(at);
        }
    }

    /** A tap on a chord, a bar, a word: there, and play. */
    playFrom(time: number): void {
        this.seek(time);
        if (!this.state.playing) void this.play();
    }

    /** What is heard now, seconds. */
    position(): number {
        if (this.audio) return this.audio.currentTime;
        if (this.sound?.loaded) return this.sound.position;
        return 0;
    }

    // ---- the loop and the trainer ---------------------------------------------------------------------

    /**
     * The loop button: off, or on around the place — the section that sounds (or its four bars where the analysis has
     * no sections), or, where there is nothing to go by, twelve seconds from a little before the place.
     */
    toggleLoop(): void {
        if (this.state.loop) {
            this.clearLoop();
            return;
        }
        const at = this.position(), piece = this.piece;
        if (piece && piece.bars.length) {
            const s = sectionAt(piece, at);
            if (s >= 0) {
                const section = piece.sections[s]!;
                this.setLoop(section.start, section.end);
                toast(t('player.loopSection', { name: section.name }));
                return;
            }
            const b = Math.max(0, barAt(piece, at)), first = b - (b % 4), last = Math.min(piece.bars.length - 1, first + 3);
            this.setLoop(piece.bars[first]!.start, piece.bars[last]!.end);
            toast(t('player.loopBars', { from: first + 1, to: last + 1 }));
            return;
        }
        const start = Math.max(0, at - PLAIN_LOOP_BEFORE), end = Math.min(this.state.duration, start + PLAIN_LOOP_LENGTH);
        this.setLoop(start, end);
        toast(t('player.loopSeconds', { count: Math.round(end - start) }));
    }

    setLoop(start: number, end: number): void {
        if (end - start < MIN_LOOP_SECONDS) return;
        this.passes = 0;
        const loop = { start, end: Math.min(end, this.state.duration) };
        if (this.sound?.loaded && !this.audio) {
            this.sound.setLoop(loop.start, loop.end, this.countsIn());
            const at = this.sound.transportPosition;
            if (at < loop.start || at >= loop.end) this.sound.seek(loop.start);
        }
        if (this.audio && (this.audio.currentTime < loop.start || this.audio.currentTime >= loop.end)) this.audio.currentTime = loop.start;
        this.set({ loop });
    }

    clearLoop(): void {
        this.sound?.clearLoop();
        this.stopCount();
        this.set({ loop: null, trainer: false });
    }

    /** One pass of the loop is over: the trainer speeds up by 5 % every two passes, up to 100 %. */
    private passed(): void {
        this.passes++;
        if (this.state.trainer && this.passes % 2 === 0 && this.state.tempo < 100) {
            const tempo = Math.min(100, this.state.tempo + 5);
            this.setTempo(tempo);
            toast(t('player.trainerTempo', { tempo }));
        }
    }

    setTempo(percent: number): void {
        const tempo = Math.max(50, Math.min(150, Math.round(percent)));
        if (this.sound?.loaded) this.sound.tempo = tempo / 100;
        for (const element of [this.audio, this.video]) if (element) element.playbackRate = tempo / 100;
        this.set({ tempo });
    }

    /** The trainer: the loop (made now if there is none) starts slower and speeds up. */
    setTrainer(on: boolean): void {
        if (on) {
            if (!this.state.loop) this.toggleLoop();
            this.passes = 0;
            this.setTempo(Math.min(this.state.tempo, 80));
        }
        this.set({ trainer: on });
    }

    // ---- the count-in ---------------------------------------------------------------------------------

    setCountIn(on: boolean): void {
        this.set({ countIn: on });
        const loop = this.state.loop;
        if (loop && this.sound?.loaded && !this.audio) this.sound.setLoop(loop.start, loop.end, this.countsIn());
    }

    /**
     * A bar's worth of clicks, then the music from where it rests (the loop's start, at the end). The clicks are laid
     * on the music's own clock once it is started: the last one falls a beat before the first beat it plays is heard
     * (the sources start on a block's edge and fade in before the place, so the moment asked for is not the moment
     * heard).
     */
    private async countAndPlay(sound: Player): Promise<void> {
        const piece = this.piece, loop = this.state.loop;
        if (!piece || !loop) return;
        const rest = sound.transportPosition, from = rest >= sound.duration - 1e-3 || rest >= loop.end - 1e-3 ? loop.start : rest;
        const first = piece.beats.find(b => b.t >= from - 1e-3)?.t ?? from;
        const bar = piece.bars[Math.max(0, barAt(piece, first + 0.001))]!;
        const tempo = this.state.tempo / 100;
        const beat = (bar.end - bar.start) / Math.max(1, bar.beats.length) / tempo;
        const count = Math.max(1, bar.lacks?.end === 'start' ? bar.beats.length + bar.lacks.beats : bar.beats.length);
        await sound.context.resume();
        await sound.play(sound.soonestStart() + 0.05 + Math.max(0, count * beat - (first - from) / tempo));
        const heard = sound.heardAt(first);
        if (heard === null) return;
        const destination = sound.context.destination;
        this.countSources = [];
        for (let i = 0; i < count; i++) this.countSources.push(...playClick(sound.context, destination, heard - (count - i) * beat, i === 0));
        this.set({ counting: true });
        // (the bar of clicks is over when the music's first beat is heard: a silent source ends then, on the audio clock)
        const over = sound.context.createConstantSource();
        over.offset.value = 0;
        over.connect(sound.context.destination);
        over.addEventListener('ended', () => {
            over.disconnect();
            if (this.countEnd !== over) return;
            this.countEnd = null;
            this.set({ counting: false });
        }, { once: true });
        over.start();
        over.stop(heard);
        this.countEnd = over;
    }

    private stopCount(): void {
        this.countEnd = null;
        for (const source of this.countSources) source.stop();
        this.countSources = [];
        if (this.state.counting) this.set({ counting: false });
    }

    // ---- watching a stream: its loop, the clip ------------------------------------------------------------

    /** On a worker's timer: it keeps its period in a hidden tab, where the page's own timers are slowed. */
    private startWatch(): void {
        if (this.watch !== null) return;
        this.watch = new TickWorker();
        this.watch.onmessage = () => this.look();
        this.watch.postMessage(WATCH_MS);
    }

    private stopWatch(): void {
        if (this.watch === null) return;
        this.watch.terminate();
        this.watch = null;
    }

    private look(): void {
        const loop = this.state.loop;
        if (this.audio) {
            const at = this.audio.currentTime;
            if (loop && at >= loop.end) {
                this.audio.currentTime = loop.start;
                this.passed();
            }
            const video = this.video;
            if (video && this.state.clip && !this.audio.paused) {
                if (video.paused) void video.play().catch(() => undefined);
                if (Math.abs(video.currentTime - this.audio.currentTime) > CLIP_DRIFT) video.currentTime = this.audio.currentTime;
            }
        }
    }

    // ---- the instruments and the mixer ------------------------------------------------------------------

    /**
     * The recording whole, or by its instruments. Turned on, the instruments are fetched and decoded (the progress is
     * shown; the recording plays meanwhile) and then take over without a stop, locked to the sample; turned off, the
     * recording takes over again and the instruments are let go, to free their memory.
     */
    setByStems(on: boolean): void {
        this.set({ byStems: on });
        const sound = this.sound;
        if (on) {
            if (sound?.stems) sound.byStems = true;
            else if (!this.state.stemsLoading && this.stemUrls.length) void this.loadStems();
        } else {
            this.stemsAsked++;
            if (sound?.loaded) sound.releaseStems();
            this.set({ stemsLoading: null, stemsReady: false });
        }
        this.applyMix();
    }

    /** The files come together; they are decoded one after another (decoding holds the compressed file too). */
    private async loadStems(): Promise<void> {
        const generation = this.generation, asked = ++this.stemsAsked;
        const current = () => generation === this.generation && asked === this.stemsAsked;
        try {
            if (Date.now() > this.expiresAt - EXPIRY_MARGIN_MS) await this.freshMedia();
            if (!current()) return;
            const total = this.stemUrls.length;
            this.set({ stemsLoading: { done: 0, total }, stemsReady: false });
            const sound = await this.getSound();
            const files = this.stemUrls.map(({ url }) => fetchBytes(url));
            files.forEach(file => file.catch(() => undefined));
            const stems: Stem[] = [];
            for (const [i, { stem }] of this.stemUrls.entries()) {
                const buffer = await sound.decode(await files[i]!);
                if (!current()) return;
                stems.push({ name: stem, buffer });
                this.set({ stemsLoading: { done: stems.length, total } });
            }
            // (they go with the decoded recording: the stream hands over first)
            await this.decoded?.catch(() => undefined);
            if (!current() || !sound.loaded) return;
            sound.attachStems(stems);
            sound.byStems = true;
            this.set({ stemsLoading: null, stemsReady: true });
            this.applyMix();
        } catch (error) {
            if (!current()) return;
            this.set({ stemsLoading: null, stemsReady: false, byStems: false });
            toast(playerErrorText(error));
        }
    }

    /** A row of the mixer: its switch and its volume. */
    setRow(name: RowName, patch: Partial<MixRow>): void {
        const mix = { ...this.state.mix, [name]: { ...this.state.mix[name], ...patch } };
        const solo = this.state.solo === name && patch.on === false ? null : this.state.solo;
        this.set({ mix, solo });
        this.applyMix();
    }

    /** "Only this": of the instruments only this one sounds; again, all as they were. */
    setSolo(name: MediaStem | null): void {
        const solo = this.state.solo === name ? null : name;
        const mix = solo ? { ...this.state.mix, [solo]: { ...this.state.mix[solo], on: true } } : this.state.mix;
        this.set({ solo, mix });
        this.applyMix();
    }

    private applyMix(): void {
        const { mix, solo } = this.state;
        if (this.audio) this.audio.volume = mix.recording.on ? mix.recording.volume : 0;
        const sound = this.sound;
        if (!sound) return;
        const level = (name: string, value: MixRow, sounding: boolean) => {
            if (!sound.hasChannel(name)) return;
            const channel = sound.channel(name);
            channel.gain = value.volume;
            channel.muted = !sounding;
        };
        level('track', mix.recording, mix.recording.on);
        for (const stem of stemOrder) level(stem, mix[stem], mix[stem].on && (!solo || solo === stem));
        level('metronome', mix.metronome, mix.metronome.on);
        level('chords', mix.chords, mix.chords.on);
    }

    // ---- the clip ---------------------------------------------------------------------------------------

    /** The clip's video element, put into the page by the clip's box. */
    clipElement(): HTMLVideoElement | null {
        return this.video;
    }

    async setClip(on: boolean): Promise<void> {
        const source = this.state.source;
        if (source?.kind !== 'track' || !this.state.hasVideo) return;
        if (on && (!this.video || Date.now() > this.videoExpiresAt - EXPIRY_MARGIN_MS)) {
            const address = await trackMedia(source.track.service, source.track.id, 'video', source.route);
            this.videoExpiresAt = Date.parse(address.expiresAt);
            if (!this.video) {
                const video = Object.assign(document.createElement('video'), { muted: true, playsInline: true, preload: 'auto' });
                video.addEventListener('playing', () => { this.clipRetried = false; });
                video.addEventListener('error', () => { if (this.video === video) void this.recoverClip(video, this.generation); });
                this.video = video;
            }
            this.video.src = address.url;
            this.video.playbackRate = this.state.tempo / 100;
            this.video.currentTime = this.position();
        }
        if (!on) this.video?.pause();
        this.set({ clip: on });
        if (on && this.state.playing) this.startWatch();
    }

    /** The clip failed: its address is asked for anew once; failing again, the box goes and the sound plays on. */
    private async recoverClip(video: HTMLVideoElement, generation: number): Promise<void> {
        const source = this.state.source;
        if (source?.kind !== 'track') return;
        if (this.clipRetried) {
            video.pause();
            this.set({ clip: false });
            toast(t('player.clipFailed'));
            return;
        }
        this.clipRetried = true;
        try {
            const address = await trackMedia(source.track.service, source.track.id, 'video', source.route);
            if (generation !== this.generation || this.video !== video) return;
            this.videoExpiresAt = Date.parse(address.expiresAt);
            video.src = address.url;
            video.playbackRate = this.state.tempo / 100;
            video.currentTime = this.position();
        } catch {
            if (generation !== this.generation) return;
            video.pause();
            this.set({ clip: false });
            toast(t('player.clipFailed'));
        }
    }

    // ---- scheduled sounds -------------------------------------------------------------------------------

    private *clicks(from: number, to: number) {
        for (const beat of this.piece?.beats ?? []) {
            if (beat.t < from) continue;
            if (beat.t >= to) break;
            yield { time: beat.t, start: (when: number, destination: AudioNode, info: { context: BaseAudioContext }) => playClick(info.context, destination, when, beat.down) };
        }
    }

    /** The chords on the app's one sound of a chord: plucked strings, each ringing until the next chord. */
    private *chordSounds(from: number, to: number) {
        for (const chord of this.piece?.chords ?? []) {
            if (chord.start < from) continue;
            if (chord.start >= to) break;
            if (!chord.pitches.length) continue;
            yield {
                time: chord.start,
                start: (when: number, destination: AudioNode, info: { context: BaseAudioContext; tempo: number }) =>
                    pluckChord(info.context, destination, when, chord.pitches, (chord.end - chord.start) / info.tempo),
            };
        }
    }
}

/** How long the sound (its audio worklet) may take to start before the page says it could not. */
const SOUND_START_MS = 15_000;

/**
 * The sound started, or a failure said within SOUND_START_MS: a browser that never loads the audio worklet would
 * otherwise leave "loading" standing forever. A sound that starts later still is let go.
 */
function startInTime(starting: Promise<Player>): Promise<Player> {
    return new Promise((resolve, reject) => {
        let late = false;
        const timer = setTimeout(() => {
            late = true;
            reject(new PlayerError('no-output', 'the sound did not start in time'));
        }, SOUND_START_MS);
        starting.then(sound => {
            clearTimeout(timer);
            if (late) void sound.dispose();
            else resolve(sound);
        }, error => {
            clearTimeout(timer);
            if (!late) reject(error);
        });
    });
}

/** What the sound says went wrong, in the person's language. */
function playerErrorText(error: unknown): string {
    if (error instanceof PlayerError)
        return error.code === 'gesture-required' ? t('player.tapToPlay') : t(`player.errors.${error.code}`);
    if (error instanceof ApiError) return errorText(error);
    return t('player.errors.other', { message: error instanceof Error ? error.message : String(error) });
}

/** A media address's bytes, whole. */
async function fetchBytes(url: string): Promise<ArrayBuffer> {
    const response = await fetch(url, { credentials: 'same-origin' });
    if (!response.ok) throw new ApiError(response.status, response.status === 410 ? 'media_expired' : 'no_media');
    return response.arrayBuffer();
}

export const player = new PlayerController();

export function usePlayer(): PlayerState {
    return useSyncExternalStore(player.subscribe, player.getState);
}

/** Calls `onTime(position, playing)` every animation frame while mounted. */
export function useClock(onTime: (position: number, playing: boolean) => void): void {
    useEffect(() => {
        let frame = 0;
        const step = () => {
            onTime(player.position(), player.getState().playing);
            frame = requestAnimationFrame(step);
        };
        frame = requestAnimationFrame(step);
        return () => cancelAnimationFrame(frame);
    }, [onTime]);
}
