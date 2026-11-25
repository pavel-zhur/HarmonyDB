// How the open chords are shown now — their key (transposition), ♭ or ♯, simpler chords, degrees, small signs — for
// the other views of the same chords (an analysis's time line, grid, lead sheet…) to name the chords as the sheet does.
//
//   const view = useChordsView();                    // inside the chords' area (ChordsView provides it)
//   chordName('C#7/E#', view)                        // "D7/F#" with +1 and ♯: the name as the sheet would write it
//   chordNameHtml('Cm7b5', view)                     // the same as the sheet draws it: "Cm<sub>7</sub><sup>b5</sup>"
//   keyName({ tonic: 'F#', isMinor: true }, view)    // a key named as the chords are: "Gm" with +1 and ♭

import { createContext, useContext } from 'react';
import { NodeChord, NodeHtml, NodeText, RepresentationSettings, SheetKey } from '../lib/representations';

const ChordsViewContext = createContext<RepresentationSettings>(new RepresentationSettings());

export const ChordsViewProvider = ChordsViewContext.Provider;

export function useChordsView(): RepresentationSettings {
    return useContext(ChordsViewContext);
}

/** A chord written as text, drawn as the sheet draws it under these settings (HTML: the name with its sub and sup). */
export function chordNameHtml(text: string, view: RepresentationSettings): string {
    // a sheet of the one chord: the same parsing, transposition, spelling and drawing as the sheet's
    const sheet = new NodeHtml({ name: 'span', children: [new NodeChord({ children: [new NodeText(text).asChild()] }).asChild()] });
    const rendered = sheet.asHtml(view.with({ customAttributes: new Map(), showDegrees: false }));
    return /<b class='chord'[^>]*>(.*)<\/b>/s.exec(rendered)?.[1] ?? '';
}

/** A chord written as text, named as the sheet would write it under these settings (plain text). */
export function chordName(text: string, view: RepresentationSettings): string {
    return chordNameHtml(text, view.with({ chordSuperscripts: false }))
        .replace(/<[^>]+>/g, '')
        .replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, '\'').replace(/&amp;/g, '&');
}

/** A key named as the chords are under these settings. */
export function keyName(key: { tonic: string; isMinor: boolean }, view: RepresentationSettings): string {
    return new SheetKey(key.tonic, key.isMinor).shown(view.with({ relativeTo: null })).name;
}
