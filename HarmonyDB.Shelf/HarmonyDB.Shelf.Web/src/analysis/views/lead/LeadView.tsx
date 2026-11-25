// The lead sheet: the piece's bars on a staff — every chord written as long as it lasts, as a rhythm slash or as its
// own notes — the chord symbols above the beats where the chords start, the sung words below, with the key signature,
// the meter of every bar, the sections and the tempo. The bar, the beat and the note that sound are lit; a tap on a beat
// plays from there. It fills the width of its box and is drawn again when that changes; a bar wider than the box even
// alone on its line is the one case the sheet scrolls sideways.
//
//   music.ts     what the sheet says: bars, meters, chords at their beats, the notes that say their lengths, words
//   rhythm.ts    the written lengths: what fills a bar, beat by beat, as note values, rests and ties
//   voicing.ts   a chord's written notes on the staff, and the accidentals a bar needs for them
//   notation.ts  the engraving of one note or rest, of beams, ties and tuplet figures
//   layout.ts    the engraving for one width: every position, the lines' breaks
//   render.ts    the SVG
//   names.ts     the names of chords and keys, as the chord sheet names them now

import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Seg } from '../../../kit/controls';
import type { Lyrics } from '../../../data/analysis';
import { player, useClock } from '../../../player/controller';
import { barAt, type Piece } from '../../piece';
import { useRedraw } from '../../redraw';
import { MUSIC_FONT } from './glyphs';
import { layoutSheet, type Writing } from './layout';
import { musicOf, type Music } from './music';
import { useNames } from './names';
import { renderSheet, type BarRef, type SheetWords } from './render';
import './lead.css';

/** One staff space, px. */
const SPACE = 8;

const writingKey = 'shelf.analysis.leadWriting';

function storedWriting(): Writing {
    try {
        return localStorage.getItem(writingKey) === 'notes' ? 'notes' : 'slashes';
    } catch {
        return 'slashes';
    }
}

interface Drawn { width: number; music: Music; writing: Writing; words: SheetWords; bars: BarRef[] }

export function LeadView({ piece, lyrics }: { piece: Piece; lyrics: Lyrics | null }) {
    const { t } = useTranslation();
    const names = useNames();
    const music = useMemo(() => musicOf(piece, lyrics, names), [piece, lyrics, names]);
    const [chosen, setChosen] = useState<Writing>(storedWriting);
    // (the written notes are in the key the chords are given in: in another key, slashes)
    const notesPossible = music.written && !names.transposed;
    const writing: Writing = notesPossible ? chosen : 'slashes';
    const unwritable = writing === 'notes' && music.bars.some(bar => bar.notes.some(note => !note.rest && !note.sound));
    const choose = (next: Writing) => {
        setChosen(next);
        try {
            localStorage.setItem(writingKey, next);
        } catch {
            // kept until the page closes
        }
    };

    // the music font and the text font in the page: the sheet is measured and drawn with them
    const [font, setFont] = useState<'loading' | 'ready' | 'failed'>('loading');
    useEffect(() => {
        let live = true;
        Promise.all([document.fonts.load(`${4 * SPACE}px ${MUSIC_FONT}`), document.fonts.ready]).then(
            () => live && setFont('ready'),
            () => live && setFont('failed'),
        );
        return () => {
            live = false;
        };
    }, []);

    const words = useMemo<SheetWords>(() => ({ free: t('analysis.lead.free'), beat: (bar, beat) => t('analysis.lead.beat', { bar, beat }) }), [t]);
    const box = useRef<HTMLDivElement>(null);
    const drawn = useRef<Drawn | null>(null);
    const lit = useRef({ bar: -1, beat: -1 });
    const widths = useRef(new Map<string, number>());
    const canvas = useRef<CanvasRenderingContext2D | null>(null);

    const draw = useCallback(() => {
        const el = box.current;
        if (!el || font !== 'ready') return;
        const width = Math.floor(el.clientWidth), was = drawn.current;
        if (was && was.width === width && was.music === music && was.writing === writing && was.words === words) return;
        drawn.current = null;
        lit.current = { bar: -1, beat: -1 };
        if (unwritable || width <= 0) {
            el.replaceChildren();
            return;
        }
        const family = getComputedStyle(el).fontFamily;
        canvas.current ??= document.createElement('canvas').getContext('2d');
        const ctx = canvas.current;
        if (!ctx) throw new Error('No 2d canvas to measure the words with.');
        const measure = (text: string, px: number, weight: number) => {
            const key = `${family}|${weight}|${px}|${text}`;
            let w = widths.current.get(key);
            if (w === undefined) {
                ctx.font = `${weight} ${px}px ${family}`;
                w = ctx.measureText(text).width;
                widths.current.set(key, w);
            }
            return w;
        };
        const sheet = layoutSheet(music, { width, space: SPACE, measure, writing });
        const out = renderSheet(sheet, music, words);
        el.replaceChildren(...out.systems);
        drawn.current = { width, music, writing, words, bars: out.bars };
    }, [font, music, writing, unwritable, words]);
    useRedraw(box, draw);

    const light = (bars: BarRef[], at: { bar: number; beat: number }, on: boolean) => {
        const ref = bars[at.bar];
        if (!ref) return;
        ref.background.classList.toggle('now', on);
        const beat = ref.beats[at.beat];
        if (!beat) return;
        beat.background.classList.toggle('now', on);
        for (const mark of beat.marks) mark.classList.toggle('now', on);
    };
    const onTime = useCallback((position: number) => {
        const d = drawn.current;
        if (!d) return;
        const bar = barAt(piece, position);
        let beat = -1;
        const beats = d.music.bars[bar]?.beats ?? [];
        for (let k = 0; k < beats.length && beats[k]! <= position; k++) beat = k;
        if (bar === lit.current.bar && beat === lit.current.beat) return;
        light(d.bars, lit.current, false);
        lit.current = { bar, beat };
        light(d.bars, lit.current, true);
    }, [piece]);
    useClock(onTime);

    const press = (target: EventTarget) => {
        const hit = target instanceof Element ? target.closest('.ls-hit') : null;
        const time = hit?.getAttribute('data-t');
        if (time) player.playFrom(Number(time));
    };
    const onKey = (e: KeyboardEvent<HTMLDivElement>) => {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        if (!(e.target instanceof Element) || !e.target.classList.contains('ls-hit')) return;
        e.preventDefault();
        press(e.target);
    };

    return (
        <div className="ls-root">
            {notesPossible && (
                <Seg value={writing} options={[['slashes', t('analysis.lead.slashes')], ['notes', t('analysis.lead.notes')]]} onChange={choose} />
            )}
            {unwritable && <p className="ls-message">{t('analysis.lead.noNotes')}</p>}
            {font === 'failed' && <p className="ls-message">{t('analysis.lead.fontFailed')}</p>}
            <div className="ls-sheet" ref={box} onClick={(e: MouseEvent<HTMLDivElement>) => press(e.target)} onKeyDown={onKey} />
        </div>
    );
}
