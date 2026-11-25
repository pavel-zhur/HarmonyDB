// A part's name, as the person reads it. The analysis names its parts in English. In English that name is kept; in
// another language the part is named by what it is: its General MIDI instrument's family, else the instrument of
// the recording it was taken from, else its own name as it came.
//
//   const partName = usePartName();
//   partName({ name: 'Electric Guitar', program: 27 })   → "Electric Guitar" / "Гитара"
//   numberRepeats(names)                                   → the same names told apart by a number

import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import type { MediaStem } from '../data/media';

/** The sixteen families of General MIDI, by program number (0–127) / 8. */
const FAMILIES = [
    'piano', 'chromatic', 'organ', 'guitar', 'bass', 'strings', 'ensemble', 'brass',
    'reed', 'pipe', 'synthLead', 'synthPad', 'synthEffects', 'ethnic', 'percussive', 'effects',
] as const;

export interface PartIdentity {
    name: string;
    /** General MIDI program, 0–127 */
    program?: number | null;
    stem?: MediaStem | null;
}

export function usePartName(): (part: PartIdentity) => string {
    const { t, i18n } = useTranslation();
    const english = i18n.language === 'en';
    return useCallback((part: PartIdentity) => {
        if (english) return part.name;
        const program = part.program;
        if (program != null && program >= 0 && program < 128) return t(`analysis.family.${FAMILIES[Math.floor(program / 8)]!}`);
        if (part.stem) return t(`player.stem.${part.stem}`);
        return part.name;
    }, [english, t]);
}

/** Names that repeat (five parts of strings) numbered in their order: "Strings 1", "Strings 2"; the others as they are. */
export function numberRepeats(names: readonly string[]): string[] {
    const total = new Map<string, number>(), seen = new Map<string, number>();
    for (const name of names) total.set(name, (total.get(name) ?? 0) + 1);
    return names.map(name => {
        if (total.get(name)! < 2) return name;
        const n = (seen.get(name) ?? 0) + 1;
        seen.set(name, n);
        return `${name} ${n}`;
    });
}
