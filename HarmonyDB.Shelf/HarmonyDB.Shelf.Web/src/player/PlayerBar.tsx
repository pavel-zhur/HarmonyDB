// The player at the bottom of a song's page (and of a track's): where the music is, play and pause, the time; the loop,
// the tempo with the trainer, the sound — whole or by instruments, each with its switch, volume and "only this", the
// metronome, the chords' sound, the count-in before the loop —, and the clip.
//
//   <PlayerBar source={…} piece={…} />   opens the source in the page's player; leaving the page stops it
//   <ClipBox />                          the clip, where the page shows it (above the bar)

import { useCallback, useEffect, useRef, useState, type PointerEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../kit/Icon';
import { MenuItem, MenuTitle, Popover } from '../kit/Popover';
import { Seg, Switch } from '../kit/controls';
import type { Piece } from '../analysis/piece';
import { player, stemOrder, useClock, usePlayer, type PlayerSource, type PlayerState, type RowName } from './controller';
import './player.css';

const clock = (seconds: number) => {
    const s = Math.max(0, Math.floor(seconds));
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
};

export function PauseIcon() {
    return <svg className="ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M7.5 5v14M16.5 5v14" strokeWidth="3.2" /></svg>;
}

/** The source of the page in the player: opened when the bar shows it, stopped when the page goes. */
export function PlayerBar({ source, piece }: { source: PlayerSource | null; piece?: Piece | null }) {
    const { t } = useTranslation();
    const state = usePlayer();
    const key = source ? (source.kind === 'analysis' ? source.chordsId : `${source.track.service}:${source.track.id}`) : null;

    useEffect(() => {
        if (!source) return;
        player.open(source);
        return () => player.close(source);
        // the source object is new on every render; its identity is its key
    }, [key]);

    useEffect(() => {
        if (source?.kind === 'analysis') player.setPiece(piece ?? null);
    }, [key, piece]);

    const fill = useRef<HTMLElement>(null);
    const time = useRef<HTMLSpanElement>(null);
    const onTime = useCallback((position: number) => {
        const duration = player.getState().duration;
        if (fill.current) fill.current.style.width = duration ? `${Math.min(100, position / duration * 100)}%` : '0';
        if (time.current) time.current.textContent = `${clock(position)} / ${clock(duration)}`;
    }, []);
    useClock(onTime);

    const [pop, setPop] = useState<{ which: 'tempo' | 'mixer'; anchor: HTMLElement } | null>(null);
    const open = (which: 'tempo' | 'mixer') => (e: React.MouseEvent<HTMLButtonElement>) => setPop(pop?.which === which ? null : { which, anchor: e.currentTarget });

    if (!source || state.source === null) return null;
    const title = source.kind === 'analysis' ? source.title : source.track.title ?? '';
    const sub = source.kind === 'analysis' ? source.subtitle : source.track.artists.map(a => a.name).filter(Boolean).join(', ');
    const ready = state.status === 'ready', failed = state.status === 'failed';
    const playing = state.playing || state.counting;

    const seekAt = (e: PointerEvent<HTMLDivElement>) => {
        const box = e.currentTarget.getBoundingClientRect();
        if (ready && state.duration) player.seek((e.clientX - box.left) / box.width * state.duration);
    };

    return (
        <div className="pl-bar">
            <div className="pl-track" title={t('player.seek')} role="slider" aria-label={t('player.seek')} aria-valuemin={0} aria-valuemax={Math.round(state.duration)}
                onPointerDown={e => {
                    e.currentTarget.setPointerCapture(e.pointerId);
                    seekAt(e);
                }}
                onPointerMove={e => { if (e.buttons) seekAt(e); }}>
                {state.loop && state.duration > 0 && (
                    <span className="lp" style={{ left: `${state.loop.start / state.duration * 100}%`, width: `${(state.loop.end - state.loop.start) / state.duration * 100}%` }} />
                )}
                <i ref={fill} />
            </div>
            <div className="pl-row2">
                <button type="button" className="pl-play" disabled={!ready && !failed} aria-label={t(failed ? 'common.retry' : playing ? 'player.pause' : 'player.play')}
                    title={t(failed ? 'common.retry' : playing ? 'player.pause' : 'player.play')} onClick={() => player.toggle()}>
                    {playing ? <PauseIcon /> : <Icon name="play" />}
                </button>
                <span className="pl-info">
                    <b>{title}</b>
                    <span>{state.status === 'loading' ? t('player.loading') : state.status === 'failed' ? state.error : state.counting ? t('player.counting') : sub}</span>
                </span>
                <span className="pl-time" ref={time} />
                <button type="button" className={state.loop ? 'pl-btn on' : 'pl-btn'} disabled={!ready} aria-pressed={!!state.loop}
                    title={t(state.loop ? 'player.loopOff' : 'player.loop')} aria-label={t('player.loop')} onClick={() => player.toggleLoop()}>
                    <Icon name="loop" />
                </button>
                <button type="button" className={state.tempo !== 100 || state.trainer ? 'pl-btn on' : 'pl-btn'} title={t('player.tempo')} onClick={open('tempo')}>{state.tempo}%</button>
                <button type="button" className={mixerChanged(state) ? 'pl-btn on' : 'pl-btn'} title={t('player.sound')} aria-label={t('player.sound')} onClick={open('mixer')}>
                    <Icon name="mixer" />
                </button>
                {state.hasVideo && (
                    <button type="button" className={state.clip ? 'pl-btn on' : 'pl-btn'} aria-pressed={state.clip} title={t('player.clip')} aria-label={t('player.clip')}
                        onClick={() => void player.setClip(!state.clip)}>
                        <Icon name="yt" />
                    </button>
                )}
            </div>
            <Popover anchor={pop?.which === 'tempo' ? pop.anchor : null} onClose={() => setPop(null)} width={340} label={t('player.tempo')}>
                <TempoMenu state={state} />
            </Popover>
            <Popover anchor={pop?.which === 'mixer' ? pop.anchor : null} onClose={() => setPop(null)} width={380} label={t('player.sound')}>
                <MixerMenu state={state} />
            </Popover>
        </div>
    );
}

function mixerChanged(state: PlayerState): boolean {
    return state.byStems || !!state.solo || state.mix.metronome.on || state.mix.chords.on || state.mix.recording.volume !== 1;
}

function TempoMenu({ state }: { state: PlayerState }) {
    const { t } = useTranslation();
    return (
        <>
            <MenuTitle>{t('player.tempo')}</MenuTitle>
            <div className="pl-tempo">
                <span className="step">
                    <button type="button" aria-label={t('player.slower')} onClick={() => player.setTempo(state.tempo - 5)}>−</button>
                    <span className="v">{state.tempo}%</span>
                    <button type="button" aria-label={t('player.faster')} onClick={() => player.setTempo(state.tempo + 5)}>+</button>
                </span>
                {state.tempo !== 100 && <button type="button" className="lnk" onClick={() => player.setTempo(100)}>{t('player.tempoBack')}</button>}
            </div>
            <p className="note pl-note">{t('player.tempoNote')}</p>
            <hr />
            <MenuItem check on={state.trainer} sub={t('player.trainerSub')} onClick={() => player.setTrainer(!state.trainer)}>{t('player.trainer')}</MenuItem>
        </>
    );
}

function MixerMenu({ state }: { state: PlayerState }) {
    const { t } = useTranslation();
    const analysis = state.source?.kind === 'analysis';
    const line = (name: RowName, label: string, sub = false) => {
        // with one instrument alone, the others are silent and look so; a tap on one of them lets them all sound again
        const silenced = sub && !!state.solo && state.solo !== name;
        return (
        <div className={sub ? 'pl-mxr sub' : 'pl-mxr'} key={name}>
            <span className="pl-mxn">{label}</span>
            <Switch on={state.mix[name].on && !silenced} label={label} onChange={on => {
                if (silenced) player.setSolo(null);
                player.setRow(name, { on });
            }} />
            <input type="range" min={0} max={100} value={Math.round(state.mix[name].volume * 100)} aria-label={t('player.volumeOf', { name: label })}
                onChange={e => player.setRow(name, { volume: Number(e.target.value) / 100 })} />
            {sub
                ? <button type="button" className={state.solo === name ? 'pl-btn on' : 'pl-btn'} aria-pressed={state.solo === name} title={t('player.only')}
                    onClick={() => player.setSolo(name as typeof stemOrder[number])}>{t('player.onlyShort')}</button>
                : <span />}
        </div>
        );
    };
    return (
        <>
            <MenuTitle>{t('player.sound')}</MenuTitle>
            <div className="pl-mixer">
                {analysis && state.stems.length > 0 && (
                    <div className="pl-mxseg">
                        <Seg value={state.byStems ? 'stems' : 'whole'} onChange={v => player.setByStems(v === 'stems')}
                            options={[['whole', t('player.whole')], ['stems', t('player.byInstruments')]]} />
                    </div>
                )}
                {state.byStems
                    ? state.stems.map(stem => line(stem, t(`player.stem.${stem}`), true))
                    : line('recording', t('player.recording'))}
                {state.stemsLoading && <p className="note pl-note">{t('player.stemsLoading', { done: state.stemsLoading.done, total: state.stemsLoading.total })}</p>}
                {analysis && state.hasBeats && (
                    <>
                        {line('metronome', t('player.metronome'))}
                        {line('chords', t('player.chordSound'))}
                        <div className="pl-mxr">
                            <span className="pl-mxn">{t('player.countIn')}</span>
                            <Switch on={state.countIn} label={t('player.countIn')} onChange={on => player.setCountIn(on)} />
                            <span className="muted small pl-mxwide">{t('player.countInSub')}</span>
                        </div>
                    </>
                )}
            </div>
            {!analysis && <p className="note pl-note">{t('player.afterAnalysis')}</p>}
        </>
    );
}

/** The clip, in step with the sound, where the page shows it. */
export function ClipBox() {
    const { t } = useTranslation();
    const state = usePlayer();
    const box = useRef<HTMLDivElement>(null);
    useEffect(() => {
        const video = player.clipElement();
        if (!state.clip || !video || !box.current) return;
        box.current.appendChild(video);
        return () => video.remove();
    }, [state.clip]);
    if (!state.clip) return null;
    return <div className="pl-clip"><div className="pl-vid" ref={box} aria-label={t('player.clip')} /></div>;
}
