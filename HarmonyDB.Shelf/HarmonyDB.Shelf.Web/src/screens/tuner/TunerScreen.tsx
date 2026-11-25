// The tuner: the microphone, asked for only when the person starts it, hears a string; the note nearest to it, how
// many cents it is off (the meter) and its frequency. The sound stays on the device. The guitar's open strings sound
// as a reference on a tap; A4 can be tuned (kept in this browser).

import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { Stepper } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { detectPitch, nearestNote, noteFrequency, type Heard } from './pitch';
import './tuner.css';

const names = ['C', 'C♯', 'D', 'D♯', 'E', 'F', 'F♯', 'G', 'G♯', 'A', 'A♯', 'B'];
/** The guitar's open strings, the lowest first: MIDI numbers. */
const strings = [40, 45, 50, 55, 59, 64];
const a4Key = 'shelf.tuner.a4';

function readA4(): number {
    try {
        const kept = Number(localStorage.getItem(a4Key));
        return kept >= 415 && kept <= 466 ? kept : 440;
    } catch {
        return 440;
    }
}

type State = { kind: 'off' } | { kind: 'asking' } | { kind: 'on' } | { kind: 'failed'; reason: 'denied' | 'none' | 'other' };

export function TunerScreen() {
    const { t } = useTranslation();
    const [state, setState] = useState<State>({ kind: 'off' });
    const [heard, setHeard] = useState<Heard | null>(null);
    const [a4, setA4] = useState(readA4);
    const [reference, setReference] = useState<number | null>(null);
    const a4Now = useRef(a4);
    a4Now.current = a4;
    const audio = useRef<{ context: AudioContext; stream?: MediaStream; frame?: number; oscillator?: OscillatorNode } | null>(null);

    const context = () => (audio.current ??= { context: new AudioContext() }).context;

    const stopListening = () => {
        const a = audio.current;
        if (!a) return;
        if (a.frame) cancelAnimationFrame(a.frame);
        a.stream?.getTracks().forEach(track => track.stop());
        a.stream = undefined;
        a.frame = undefined;
    };

    const stopReference = () => {
        audio.current?.oscillator?.stop();
        if (audio.current) audio.current.oscillator = undefined;
        setReference(null);
    };

    // everything stops with the page: the microphone, the tone, the context
    useEffect(() => () => {
        stopListening();
        audio.current?.oscillator?.stop();
        void audio.current?.context.close();
        audio.current = null;
    }, []);

    const listen = async () => {
        if (!navigator.mediaDevices?.getUserMedia) {
            setState({ kind: 'failed', reason: 'none' });
            return;
        }
        stopReference();
        setState({ kind: 'asking' });
        let stream: MediaStream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false } });
        } catch (error) {
            const name = error instanceof DOMException ? error.name : '';
            setState({ kind: 'failed', reason: name === 'NotAllowedError' || name === 'SecurityError' ? 'denied' : name === 'NotFoundError' ? 'none' : 'other' });
            return;
        }
        const ctx = context();
        await ctx.resume();
        const analyser = ctx.createAnalyser();
        analyser.fftSize = 4096;
        ctx.createMediaStreamSource(stream).connect(analyser);
        const samples = new Float32Array(analyser.fftSize);
        audio.current!.stream = stream;
        let last: number | null = null;
        const tick = () => {
            analyser.getFloatTimeDomainData(samples);
            const pitch = detectPitch(samples, ctx.sampleRate);
            if (pitch !== null) {
                const note = nearestNote(pitch, a4Now.current);
                // a note is shown once it is heard twice in a row: a pluck's first noise is not a note
                if (note.midi === last) setHeard(note);
                last = note.midi;
            }
            audio.current!.frame = requestAnimationFrame(tick);
        };
        tick();
        setState({ kind: 'on' });
    };

    const stop = () => {
        stopListening();
        setState({ kind: 'off' });
    };

    const play = (midi: number) => {
        if (reference === midi) {
            stopReference();
            return;
        }
        if (state.kind === 'on') stop();
        const ctx = context();
        void ctx.resume();
        const a = audio.current!;
        if (!a.oscillator) {
            a.oscillator = ctx.createOscillator();
            const gain = ctx.createGain();
            gain.gain.value = 0.2;
            a.oscillator.connect(gain).connect(ctx.destination);
            a.oscillator.start();
        }
        a.oscillator.frequency.value = noteFrequency(midi, a4Now.current);
        setReference(midi);
    };

    const changeA4 = (value: number) => {
        setA4(value);
        try {
            localStorage.setItem(a4Key, String(value));
        } catch {
            // kept for this page only
        }
        if (reference !== null && audio.current?.oscillator) audio.current.oscillator.frequency.value = noteFrequency(reference, value);
    };

    const shown = reference !== null ? { midi: reference, cents: 0, frequency: noteFrequency(reference, a4) } : heard;
    const inTune = shown !== null && Math.abs(shown.cents) <= 5;
    return (
        <Page>
            <h1>{t('tools.tuner')}</h1>
            <p className="lead">{t('tuner.lead')}</p>
            <div className="tu-box">
                <Meter cents={shown?.cents ?? 0} on={shown !== null} inTune={inTune} />
                <div className={inTune ? 'tu-note in' : 'tu-note'} aria-live="polite">
                    {shown ? <>{names[shown.midi % 12]![0]}<sup>{names[shown.midi % 12]!.slice(1)}</sup><sub>{Math.floor(shown.midi / 12) - 1}</sub></> : '—'}
                </div>
                <div className="tu-hz">
                    {shown ? `${shown.frequency.toFixed(1)} ${t('tuner.hz')}` : ' '}
                    {shown && reference === null && <span className="muted"> · {shown.cents > 0 ? `+${shown.cents}` : shown.cents} {t('tuner.cents')}</span>}
                </div>
                {state.kind === 'on'
                    ? <button type="button" className="btn" onClick={stop}><Icon name="x" />{t('tuner.stop')}</button>
                    : <button type="button" className="btn pri" disabled={state.kind === 'asking'} onClick={() => void listen()}><Icon name="mic" />{t('tuner.listen')}</button>}
                {state.kind === 'off' && <p className="muted small tu-why">{t('tuner.why')}</p>}
                {state.kind === 'on' && <p className="muted small tu-why">{t('tuner.listening')}</p>}
                {state.kind === 'asking' && <p className="muted small tu-why">{t('tuner.asking')}</p>}
                {state.kind === 'failed' && <p className="failed tu-why">{t(`tuner.failed.${state.reason}`)}</p>}
            </div>

            <h2>{t('tuner.strings')}</h2>
            <div className="tu-strings">
                {strings.map(midi => (
                    <button type="button" key={midi} className={reference === midi ? 'tu-str on' : heard?.midi === midi && state.kind === 'on' ? 'tu-str near' : 'tu-str'}
                        aria-pressed={reference === midi} onClick={() => play(midi)}>
                        {names[midi % 12]}<sub>{Math.floor(midi / 12) - 1}</sub>
                    </button>
                ))}
            </div>
            <p className="muted small">{t('tuner.stringsNote')}</p>

            <div className="lines tu-set">
                <div className="setrow">
                    <span>{t('tuner.a4')}<span className="sub">{t('tuner.a4Sub')}</span></span>
                    <Stepper value={`${a4} ${t('tuner.hz')}`} lessLabel={t('tuner.lower')} moreLabel={t('tuner.higher')} resetLabel={t('profile.reset')}
                        onStep={d => changeA4(Math.min(466, Math.max(415, a4 + d)))} onReset={() => changeA4(440)} />
                </div>
            </div>
        </Page>
    );
}

/** −50 … +50 cents on an arc; the pointer at the cents heard. */
function Meter({ cents, on, inTune }: { cents: number; on: boolean; inTune: boolean }) {
    const ticks = Array.from({ length: 11 }, (_, i) => (i - 5) * 9);
    return (
        <svg className={inTune ? 'tu-meter in' : 'tu-meter'} viewBox="-110 -105 220 115" aria-hidden="true">
            {ticks.map(angle => (
                <line key={angle} className={angle === 0 ? 'mid' : angle % 45 === 0 ? 'strong' : undefined}
                    x1="0" y1={angle === 0 ? -100 : -96} x2="0" y2="-84" transform={`rotate(${angle})`} />
            ))}
            <g className={on ? 'ptr' : 'ptr off'} style={{ transform: `rotate(${Math.max(-50, Math.min(50, cents)) * 0.9}deg)` }}>
                <line x1="0" y1="0" x2="0" y2="-90" />
            </g>
            <circle r="5" />
        </svg>
    );
}
