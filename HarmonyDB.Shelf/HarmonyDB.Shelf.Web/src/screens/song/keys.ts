// Small helpers of the song page: a key's step as written ("0", "+3", "−2"), a variant's letter in the person's language.

import { i18n } from '../../i18n';

export function keyText(semitones: number): string {
    return semitones > 0 ? `+${semitones}` : semitones < 0 ? `−${-semitones}` : '0';
}

/** A step of the key, going round: below −7 comes +4, above +7 comes −4. */
export function stepKey(semitones: number, direction: -1 | 1): number {
    const next = semitones + direction;
    return next > 7 ? next - 12 : next < -7 ? next + 12 : next;
}

/** The variant's letter: А, Б, В… in Russian, A, B, C… in English; past the alphabet, its number. */
export function letter(index: number): string {
    const letters = i18n.t('song.letters');
    return index < letters.length ? letters[index]! : String(index + 1);
}
