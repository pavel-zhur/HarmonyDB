// What every view of an analysis writes the same way: a chord's name with its degree, a silence, and the marks of a
// change of key, meter or tempo. Chords and keys are named as the chord sheet names them now — its key (the person's
// transposition), ♭ or ♯, its small signs, degrees shown or not (chords/view.tsx).

import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { chordName, chordNameHtml, keyName, useChordsView } from '../chords/view';
import type { RepresentationSettings } from '../lib/representations';
import type { Change, Part } from './piece';

/** A chord's name as the sheet draws it and, small after it, its degree in the key in force (when degrees are shown). */
export function ChordLabel({ name, degree }: { name: string; degree?: string | null }) {
    const view = useChordsView();
    return (
        <span className="an-achord">
            {/* (a slash chord may stand on two lines where it is narrow, the bass under it: a break is allowed before the bass) */}
            <span className="aname" dangerouslySetInnerHTML={{ __html: chordNameHtml(name, view).replace(/(?<!<)\//g, '<wbr>/') }} />
            {degree && view.showDegrees && <small className="an-adeg">{degree.replace(/ \/ /g, '/')}</small>}
        </span>
    );
}

/** A chord's name as plain text, as the sheet names it now. */
export function useChordText(): (name: string) => string {
    const view = useChordsView();
    return useCallback((name: string) => chordName(name, view), [view]);
}

/** What a part of a beat writes: its chord's name (as given), "N.C.", a stop, or nothing. */
export function partName(part: Part): string | null {
    if (part.chord?.name) return part.chord.name;
    if (part.silence === 'noChord') return 'N.C.';
    if (part.silence === 'stop') return '‖';
    return null;
}

/** "F#m" → named as the chords are now. */
export function shownKey(key: string, view: RepresentationSettings): string {
    const minor = key.endsWith('m');
    return keyName({ tonic: minor ? key.slice(0, -1) : key, isMinor: minor }, view);
}

const noteSign: Record<number, string> = { 1: '𝅝', 2: '𝅗𝅥', 4: '♩', 8: '♪', 16: '𝅘𝅥𝅯' };

/** "♩ = 136": the tempo as a musician marks it. */
export function tempoMark(bpm: number, unit?: { denominator: number; dotted?: boolean } | null): string {
    const sign = unit ? `${noteSign[unit.denominator] ?? '♩'}${unit.dotted ? '.' : ''}` : '♩';
    return `${sign} = ${bpm}`;
}

/** The words of a change: the key, the meter, the tempo — those that change there. */
export function useChangeWords() {
    const { t } = useTranslation();
    const view = useChordsView();
    return useCallback((change: Change, first: boolean, unit?: { denominator: number; dotted?: boolean } | null): string[] => {
        const words: string[] = [];
        if (change.key !== undefined) words.push(change.key ? t(first ? 'analysis.key' : 'analysis.keyChange', { key: shownKey(change.key, view) }) : t('analysis.noKey'));
        if (change.timeSignature !== undefined) words.push(change.timeSignature === 'free' ? t('analysis.free') : change.timeSignature);
        if (change.bpm !== undefined) words.push(tempoMark(change.bpm, unit));
        return words;
    }, [t, view]);
}
