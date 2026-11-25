// A lone pitch named with its octave in the key heard at its time, as a root of that pitch is named in that key: in
// F the black key over A is Bb, in E it is A#. Without a key, one fixed name per pitch. A double sharp or flat is
// written as the plain name of its pitch (F## as G, Ebb as D), with the octave going with the letter.
//
//   pitchName(58, "F") → "Bb3"; pitchName(58, null) → "Bb3"; pitchName(58, "E") → "A#3"

/** The twelve pitch classes, C to B, without a key. */
const NO_KEY = ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'];

/** The twelve pitch classes, C to B, in each key. */
const KEY_NAMES: Record<string, string[]> = {
    'Cb': ['Dbb', 'Db', 'Ebb', 'Eb', 'Fb', 'F', 'Gb', 'Abb', 'Ab', 'Bbb', 'Bb', 'Cb'],
    'Gb': ['C', 'Db', 'Ebb', 'Eb', 'Fb', 'F', 'Gb', 'Abb', 'Ab', 'Bbb', 'Bb', 'Cb'],
    'Db': ['C', 'Db', 'Ebb', 'Eb', 'Fb', 'F', 'Gb', 'G', 'Ab', 'Bbb', 'Bb', 'Cb'],
    'Ab': ['C', 'Db', 'D', 'Eb', 'Fb', 'F', 'Gb', 'G', 'Ab', 'Bbb', 'Bb', 'Cb'],
    'Eb': ['C', 'Db', 'D', 'Eb', 'Fb', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'Cb'],
    'Bb': ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'Cb'],
    'F': ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B'],
    'C': ['C', 'Db', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'],
    'G': ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'],
    'D': ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B'],
    'A': ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B'],
    'E': ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'],
    'B': ['C', 'C#', 'D', 'D#', 'E', 'E#', 'F#', 'G', 'G#', 'A', 'A#', 'B'],
    'F#': ['B#', 'C#', 'D', 'D#', 'E', 'E#', 'F#', 'G', 'G#', 'A', 'A#', 'B'],
    'C#': ['B#', 'C#', 'D', 'D#', 'E', 'E#', 'F#', 'F##', 'G#', 'A', 'A#', 'B'],
    'Abm': ['C', 'Db', 'Ebb', 'Eb', 'Fb', 'F', 'Gb', 'G', 'Ab', 'Bbb', 'Bb', 'Cb'],
    'Ebm': ['C', 'Db', 'D', 'Eb', 'Fb', 'F', 'Gb', 'G', 'Ab', 'Bbb', 'Bb', 'Cb'],
    'Bbm': ['C', 'Db', 'D', 'Eb', 'Fb', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'Cb'],
    'Fm': ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'Cb'],
    'Cm': ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B'],
    'Gm': ['C', 'Db', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'],
    'Dm': ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'],
    'Am': ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B'],
    'Em': ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B'],
    'Bm': ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'],
    'F#m': ['C', 'C#', 'D', 'D#', 'E', 'E#', 'F#', 'G', 'G#', 'A', 'A#', 'B'],
    'C#m': ['B#', 'C#', 'D', 'D#', 'E', 'E#', 'F#', 'G', 'G#', 'A', 'A#', 'B'],
    'G#m': ['B#', 'C#', 'D', 'D#', 'E', 'E#', 'F#', 'F##', 'G#', 'A', 'A#', 'B'],
    'D#m': ['B#', 'C#', 'C##', 'D#', 'E', 'E#', 'F#', 'F##', 'G#', 'A', 'A#', 'B'],
    'A#m': ['B#', 'C#', 'C##', 'D#', 'E', 'E#', 'F#', 'F##', 'G#', 'G##', 'A#', 'B'],
};

const LETTERS = 'CDEFGAB';
const NATURAL = [0, 2, 4, 5, 7, 9, 11];
const mod = (n: number, m: number) => ((n % m) + m) % m;

/** The pitch's name with its octave: 60 is C4. */
export function pitchName(midi: number, key: string | null): string {
    const name = (key !== null ? KEY_NAMES[key] : undefined) ?? NO_KEY;
    const written = name[mod(midi, 12)]!;
    let letter = LETTERS.indexOf(written[0]!);
    let alter = written.length - 1;
    if (written[1] === 'b') alter = -alter;
    // the octave of the letter as written: Cb is the B under the C of its octave, B# the C over its B
    let octave = (midi - (NATURAL[letter]! + alter)) / 12 - 1;
    while (alter > 1) {
        alter -= mod(NATURAL[mod(letter + 1, 7)]! - NATURAL[mod(letter, 7)]!, 12);
        letter++;
    }
    while (alter < -1) {
        alter += mod(NATURAL[mod(letter, 7)]! - NATURAL[mod(letter - 1, 7)]!, 12);
        letter--;
    }
    octave += Math.floor(letter / 7);
    return LETTERS[mod(letter, 7)]! + (alter > 0 ? '#' : alter < 0 ? 'b' : '') + octave;
}
