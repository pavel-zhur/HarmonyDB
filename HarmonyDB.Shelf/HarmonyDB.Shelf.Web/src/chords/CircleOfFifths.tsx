// The circle of fifths: the old frontend's (shared/CircleOfFifths.tsx) — the song's chords lit on it (a chord's root
// as major or minor; a bass alone, paler), a tap plays the chord and offers it as the tonic, against which the sheet
// can be shown in degrees ("T = Am") — drawn as the mock-up draws it.

import { useMemo } from 'react';
import { ChordType } from '../lib/analysis/Models/ChordType';
import { Note, RepresentationSettings } from '../lib/representations';
import { chordDataParser } from './analysis';
import { playChord } from './sound';

const key = (note: number, chordType: ChordType) => `${note}|${chordType}`;

export interface Tonic {
    readonly note: number;
    readonly major: boolean;
    /** as the circle names it: "Am", "F#" */
    readonly name: string;
}

export function CircleOfFifths({ chords, onPlayed }: { chords: readonly string[]; onPlayed: (tonic: Tonic) => void }) {
    const lit = useMemo(() => {
        const pairs = new Map<string, { note: number; type: ChordType }>();
        for (const data of new Set(chords)) {
            const chord = chordDataParser.getChord(data);
            if (chord === null) continue;
            pairs.set(key(chord.root, chord.chordTypeForCircleOfFifths), { note: chord.root, type: chord.chordTypeForCircleOfFifths });
            if (chord.bass !== null) pairs.set(key(chord.bass, ChordType.Unknown), { note: chord.bass, type: ChordType.Unknown });
        }
        // a note that is a major or a minor chord is not lit again as a bass
        const byNote = new Map<number, { note: number; type: ChordType }[]>();
        for (const pair of pairs.values()) byNote.set(pair.note, [...(byNote.get(pair.note) ?? []), pair]);
        return new Set([...byNote.values()].flatMap(items => items.some(x => x.type === ChordType.Major || x.type === ChordType.Minor)
            ? items.filter(x => x.type !== ChordType.Unknown) : items).map(x => key(x.note, x.type)));
    }, [chords]);

    const plain = new RepresentationSettings();
    const tap = (note: Note, major: boolean) => {
        const name = note.representation(plain) + (major ? '' : 'm');
        playChord(`[!${note.value}!]${major ? '' : 'm'}`, name);
        onPlayed({ note: note.value, major, name });
    };
    const shade = (note: number, type: ChordType) => lit.has(key(note, type)) ? ' u' : lit.has(key(note, ChordType.Unknown)) ? ' b' : '';

    const arc = (r1: number, r2: number, a1: number, a2: number) => {
        const p = (r: number, a: number) => `${(r * Math.sin(a)).toFixed(2)} ${(-r * Math.cos(a)).toFixed(2)}`;
        return `M${p(r2, a1)}A${r2} ${r2} 0 0 1 ${p(r2, a2)}L${p(r1, a2)}A${r1} ${r1} 0 0 0 ${p(r1, a1)}Z`;
    };

    return (
        <svg className="cof" viewBox="-110 -110 220 220" role="group">
            {Note.CircleOfFifths.map(({ major, minor }, i) => {
                const a1 = (i - .5) * Math.PI / 6, a2 = (i + .5) * Math.PI / 6, a = i * Math.PI / 6;
                const majorName = major.representation(plain), minorName = minor.representation(plain) + 'm';
                return (
                    <g key={i}>
                        <path className={'seg0' + shade(major.value, ChordType.Major)} d={arc(72, 104, a1, a2)} onClick={() => tap(major, true)}><title>{majorName}</title></path>
                        <path className={'seg1' + shade(minor.value, ChordType.Minor)} d={arc(42, 72, a1, a2)} onClick={() => tap(minor, false)}><title>{minorName}</title></path>
                        <text x={(88 * Math.sin(a)).toFixed(1)} y={(-88 * Math.cos(a)).toFixed(1)}>{majorName}</text>
                        <text className="mi" x={(57 * Math.sin(a)).toFixed(1)} y={(-57 * Math.cos(a)).toFixed(1)}>{minorName}</text>
                    </g>
                );
            })}
        </svg>
    );
}
