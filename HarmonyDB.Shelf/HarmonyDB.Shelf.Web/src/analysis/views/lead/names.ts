// How the lead sheet names its chords and keys: as the chord sheet names them now (the person's transposition, ♭ or ♯),
// its accidentals then written as music signs. The one place where the sheet's names are formed.

import { useMemo } from 'react';
import { useChordsView } from '../../../chords/view';
import { shownKey, useChordText } from '../../marks';
import { withSigns } from './spelling';

export interface Names {
    /** a chord's text over the staff ("B♭m7/F") */
    chord: (name: string) => string;
    /** the key a bar is written in ("Gm"): its signature is this key's */
    key: (key: string) => string;
    /** whether the chords are written in another key than the one they are given in */
    transposed: boolean;
}

export function useNames(): Names {
    const view = useChordsView();
    const chordText = useChordText();
    return useMemo(() => ({
        chord: (name: string) => withSigns(chordText(name)),
        key: (key: string) => shownKey(key, view),
        transposed: view.transpose !== 0,
    }), [view, chordText]);
}
