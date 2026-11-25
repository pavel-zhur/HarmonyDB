// The notes heard in each part, as a piano roll: pitch upwards with a keyboard at the left edge, time to the right over
// the bars and beats, the sections and the chords along the top, a colour a part, the drums in a lane of their own along
// the bottom. The parts are switched on and off; the place that sounds is followed while the music plays; a tap plays
// from there, a drag makes a loop; the notes are given as a MIDI file too.
//
// A note is named in the key of its bar; the keyboard's names are in the key heard at the playhead.

import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Notes } from '../../../data/analysis';
import { chordsMedia } from '../../../data/media';
import { toast } from '../../../app/toasts';
import { errorText } from '../../../api/errors';
import { Icon } from '../../../kit/Icon';
import { player, useClock, usePlayer } from '../../../player/controller';
import { MIN_LOOP_SECONDS } from '../../../player/sound/types';
import { barAt, type Chord, type Piece } from '../../piece';
import { useChordText } from '../../marks';
import { useRedraw } from '../../redraw';
import { readNotes, LANE_COLOURS } from './lanes';
import { usePartName } from '../../partNames';
import { pitchName } from './names';
import { createRoll, MAX_ROW, MAX_ZOOM, MIN_ROW, MIN_ZOOM, type Roll, type RollTexts } from './roll';
import './roll.css';

const zoomKey = 'shelf.analysis.zoom';           // shared with the time line
const rowKey = 'shelf.analysis.rollRow';         // the height of a semitone; none: fitted to the height
const DEFAULT_ZOOM = 40;

function stored(key: string): number | null {
    try {
        const value = localStorage.getItem(key);
        return value === null ? null : Number(value);
    } catch {
        return null;
    }
}
function store(key: string, value: number | null) {
    try {
        if (value === null) localStorage.removeItem(key);
        else localStorage.setItem(key, String(value));
    } catch {
        // kept until the page closes
    }
}
function storedZoom(): number {
    const value = stored(zoomKey);
    return value !== null && value >= MIN_ZOOM && value <= MAX_ZOOM ? value : DEFAULT_ZOOM;
}
function storedRow(): number | null {
    const value = stored(rowKey);
    return value !== null && value >= MIN_ROW && value <= MAX_ROW ? value : null;
}

export function NotesView({ notes, piece, chordsId, share }: { notes: Notes; piece: Piece; chordsId: string; share?: string | null }) {
    const { t } = useTranslation();
    const state = usePlayer();
    const chordName = useChordText();
    const partName = usePartName();
    const data = useMemo(() => {
        const read = readNotes(notes);
        return { ...read, lanes: read.lanes.map(lane => ({ ...lane, name: partName(lane) })) };
    }, [notes, partName]);
    const [off, setOff] = useState<Set<string>>(() => new Set());
    const [zoom, setZoom] = useState<{ pps: number; row: number | null; shown: number }>(() => ({ pps: storedZoom(), row: storedRow(), shown: 0 }));
    const host = useRef<HTMLDivElement>(null);
    const roll = useRef<Roll | null>(null);
    const keyNow = useRef<string | null | undefined>(undefined);

    /** The key of the bar at a time; none before the first bar or after the last. */
    const keyAt = useCallback((time: number) => {
        const bar = barAt(piece, time);
        return bar >= 0 ? piece.bars[bar]!.key : null;
    }, [piece]);
    const chordText = useCallback((chord: Chord) => (chord.name ? chordName(chord.name) : chord.kind === 'stop' ? '‖' : 'N.C.'), [chordName]);
    const texts = useMemo((): RollTexts => ({
        drum: id => t(`analysis.roll.drum.${id}`),
        drums: t('analysis.roll.drums'),
        velocity: value => t('analysis.roll.velocity', { value }),
        seconds: value => t('analysis.roll.seconds', { value }),
        chords: t('analysis.roll.chords'),
        noChord: t('analysis.roll.noChord'),
        sections: t('analysis.roll.sections'),
        loopStart: t('analysis.roll.loopStart'),
        loopEnd: t('analysis.roll.loopEnd'),
        loopStartTitle: t('analysis.roll.loopStartTitle'),
        loopEndTitle: t('analysis.roll.loopEndTitle'),
    }), [t]);

    // what the roll calls is read when it calls: the newest of each
    const now = useRef({ keyAt, chordText });
    now.current = { keyAt, chordText };

    const saveZoom = useCallback((pps: number, row: number | null) => {
        store(zoomKey, pps);
        store(rowKey, row);
        setZoom({ pps, row, shown: roll.current?.rowShown ?? 0 });
    }, []);

    useLayoutEffect(() => {
        const el = host.current!;
        const made = createRoll(el, {
            noteName: (midi, time) => pitchName(midi, now.current.keyAt(time)),
            chordText: chord => now.current.chordText(chord),
            onSeek: time => player.playFrom(time),
            onChord: chord => player.playFrom(chord.start),
            onSection: section => player.playFrom(section.start),
            onLoop: (start, end) => player.setLoop(start, end),
            onZoom: ({ pps, row }) => saveZoom(pps, row),
            minLoop: MIN_LOOP_SECONDS,
            texts,
        });
        roll.current = made;
        made.setZoom(storedZoom());
        made.setRow(storedRow());
        return () => {
            made.destroy();
            roll.current = null;
        };
        // (made once; what changes is given to it below)
    }, []);

    useLayoutEffect(() => {
        const r = roll.current!;
        r.setSections(piece.sections);
        r.setSong({ data, length: Math.max(piece.duration, data.end), bars: piece.bars, beats: piece.beats.map(b => b.t), chords: piece.chords });
        setZoom(z => ({ ...z, shown: r.rowShown }));
    }, [data, piece]);

    const ids = useMemo(() => [...data.lanes.map(l => l.id), ...(data.drums ? [data.drums.id] : [])], [data]);
    useLayoutEffect(() => {
        const r = roll.current!;
        r.setShown(ids.filter(id => !off.has(id)));
        setZoom(z => ({ ...z, shown: r.rowShown }));
    }, [ids, off]);

    useEffect(() => roll.current!.setTexts(texts), [texts]);
    useEffect(() => roll.current!.refresh(), [chordText]);
    useEffect(() => roll.current!.setLoop(state.loop), [state.loop]);

    const redraw = useCallback(() => {
        const r = roll.current;
        if (!r) return;
        r.redraw();
        setZoom(z => ({ ...z, shown: r.rowShown }));
    }, []);
    useRedraw(host, redraw);

    const onTime = useCallback((position: number, playing: boolean) => {
        const r = roll.current;
        if (!r) return;
        r.setTime(position, playing);
        // the keyboard's names are those of the key heard at the playhead: written again where it changes
        const key = keyAt(position);
        if (key !== keyNow.current) {
            keyNow.current = key;
            r.refresh();
        }
    }, [keyAt]);
    useClock(onTime);

    const changeZoom = (pps: number) => {
        const r = roll.current!;
        r.setZoom(pps);
        saveZoom(r.zoom, r.row);
    };
    const changeRow = (row: number | null) => {
        const r = roll.current!;
        r.setRow(row === null ? null : Math.min(MAX_ROW, Math.max(MIN_ROW, row)));
        saveZoom(r.zoom, r.row);
    };

    const toggle = (id: string) => setOff(prev => {
        const next = new Set(prev);
        if (next.has(id)) next.delete(id);
        else next.add(id);
        return next;
    });

    const downloadMidi = async () => {
        try {
            const media = await chordsMedia(chordsId, share);
            if (!media.midiUrl) return;
            const a = document.createElement('a');
            a.href = media.midiUrl;
            a.download = 'notes.mid';
            a.click();
        } catch (error) {
            toast(errorText(error));
        }
    };

    const anyNotes = data.lanes.some(l => l.count) || !!data.drums?.count;
    const noneShown = ids.every(id => off.has(id));

    return (
        <div className="rl-view">
            <div className="chips" role="group" aria-label={t('analysis.roll.parts')}>
                {data.lanes.map(lane => (
                    <button type="button" key={lane.id} className={off.has(lane.id) ? 'chip' : 'chip on'} aria-pressed={!off.has(lane.id)} onClick={() => toggle(lane.id)}>
                        <i className={`rl-sw c${lane.colour % LANE_COLOURS}`} />{lane.name}
                    </button>
                ))}
                {data.drums && (
                    <button type="button" className={off.has(data.drums.id) ? 'chip' : 'chip on'} aria-pressed={!off.has(data.drums.id)} onClick={() => toggle(data.drums!.id)}>
                        <i className="rl-sw drums" />{t('analysis.roll.drums')}
                    </button>
                )}
            </div>
            <div className="rl-bar">
                <span className="muted small rl-hint">{t('analysis.roll.hint')}</span>
                <span className="muted small rl-hint-touch">{t('analysis.roll.hintTouch')}</span>
                <span className="rl-steps">
                    <span className="step">
                        <button type="button" aria-label={t('analysis.zoomOut')} onClick={() => changeZoom(zoom.pps / 1.4)}>−</button>
                        <button type="button" aria-label={t('analysis.zoomIn')} onClick={() => changeZoom(zoom.pps * 1.4)}>+</button>
                    </span>
                    <span className="step">
                        <button type="button" aria-label={t('analysis.roll.lower')} onClick={() => changeRow((zoom.shown || MIN_ROW) - 2)}>↓</button>
                        <button type="button" aria-label={t('analysis.roll.taller')} onClick={() => changeRow((zoom.shown || MIN_ROW) + 2)}>↑</button>
                    </span>
                    <button type="button" className={zoom.row === null ? 'chip on' : 'chip'} aria-pressed={zoom.row === null} title={t('analysis.roll.fitTitle')} onClick={() => changeRow(null)}>
                        {t('analysis.roll.fit')}
                    </button>
                </span>
            </div>
            <div className="rl-frame">
                <div className="rl-roll" ref={host} role="group" aria-label={t('analysis.roll.label')} />
                {(noneShown || !anyNotes) && <p className="rl-empty">{t(anyNotes ? 'analysis.roll.noneShown' : 'analysis.roll.noNotes')}</p>}
            </div>
            <p className="muted small rl-foot">
                {data.velocity && (
                    <span className="rl-about">
                        <i className="rl-swatch" />{t('analysis.roll.loud')}
                        <i className="rl-swatch faint" />{t('analysis.roll.soft')}
                    </span>
                )}
                {notes.midi && <button type="button" className="lnk" onClick={() => void downloadMidi()}><Icon name="down" />{t('analysis.roll.midi')}</button>}
            </p>
        </div>
    );
}
