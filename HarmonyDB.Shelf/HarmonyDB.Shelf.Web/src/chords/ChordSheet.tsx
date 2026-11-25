// The chord sheet as the old frontend showed it: the HTML of NodeHtml.asHtml (its lines, the chords flying over their
// words, the wrapping — made on purpose, at any width and any font) in a block of the old styles (chords.css). Two
// fingers on it change the chords' size (data-chords, --chords-scale). A tap on a chord plays it (and, when asked,
// tells the caller which chord it was: the progression's search); a tap on a timed chord or word tells the player.

import { useEffect, useMemo, useRef } from 'react';
import type { NodeHtml, RepresentationSettings } from '../lib/representations';
import { forgetPlayedChord, playChord } from './sound';
import { followTime, light, seek } from './time';
import './chords.css';

export function ChordSheet({ sheet, settings, onChord }: {
    sheet: NodeHtml;
    settings: RepresentationSettings;
    /** a chord tapped: its index in the sheet */
    onChord?: (index: number) => void;
}) {
    const html = useMemo(() => sheet.asHtml(settings), [sheet, settings]);
    const box = useRef<HTMLDivElement>(null);
    const chordTap = useRef(onChord);
    chordTap.current = onChord;

    useEffect(() => followTime(seconds => box.current && light(box.current, seconds)), [html]);

    // the fingerings of a chord tapped here do not outlive the sheet (another view, other chords, another page)
    useEffect(() => forgetPlayedChord, []);

    const tap = (e: React.MouseEvent<HTMLDivElement>) => {
        const target = e.target as Element;
        const chord = target.closest('b.chord');
        if (chord) {
            const data = chord.getAttribute('chord-data') ?? '';
            const degree = chord.querySelector('.chord-degree')?.textContent ?? '';
            const name = (chord.textContent ?? '').slice(0, (chord.textContent ?? '').length - degree.length);
            playChord(data, name);
            chordTap.current?.(Number(chord.getAttribute('chord-index')));
        }
        const timed = target.closest('[data-time]');
        if (timed && timed !== box.current) seek(Number(timed.getAttribute('data-time')));
    };

    return <div ref={box} className="sheet" data-chords onClick={tap} dangerouslySetInnerHTML={{ __html: html }} />;
}
