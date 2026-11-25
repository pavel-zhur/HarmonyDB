// The key signature of a key, and a chord name taken apart into the parts the sheet writes. Names are written as they
// are given; nothing here renames a chord or a key.
//
//   parseKey("Bbm")            → { tonic, minor, signature } or null
//       signature  sharps (> 0) or flats (< 0) in the key signature, −7 … 7
//   parseChordName("A#m7/F##") → { root, quality, bass } or null
//   withSigns(name)            → the name with ♭ ♯ 𝄫 𝄪 for its accidentals ("B♭m7♭5/F𝄪")
//
// A note is kept as its place on the line of fifths: F −1, C 0, G 1, D 2, A 3, E 4, B 5; a sharp adds 7, a flat takes 7.

export interface Key {
    tonic: string;
    minor: boolean;
    signature: number;
}

const FIFTHS_OF_LETTER: Record<string, number> = { F: -1, C: 0, G: 1, D: 2, A: 3, E: 4, B: 5 };

/** Music signs back to letters: "F♯m" → "F#m". */
function plain(text: string): string {
    return text.replace(/♯/g, '#').replace(/♭/g, 'b').replace(/𝄪/g, '##').replace(/𝄫/g, 'bb');
}

/** A key named with up to seven sharps or flats; G♯ major, eight, is not one. */
export function parseKey(text: string | null): Key | null {
    const m = /^([A-G])([#b]?)(m?)$/.exec(plain(text ?? '').trim());
    if (!m) return null;
    const minor = m[3] === 'm';
    const signature = FIFTHS_OF_LETTER[m[1]!]! + 7 * (m[2] === '#' ? 1 : m[2] === 'b' ? -1 : 0) - (minor ? 3 : 0);
    if (Math.abs(signature) > 7) return null;
    return { tonic: m[1]! + m[2]!, minor, signature };
}

export interface ChordParts {
    root: string;
    quality: string;
    bass: string | null;
}

export function parseChordName(name: string): ChordParts | null {
    const m = /^([A-G](?:#+|b+)?)(.*?)(?:\/([A-G](?:#+|b+)?))?$/.exec(plain(name).trim());
    if (!m) return null;
    return { root: m[1]!, quality: m[2]!, bass: m[3] ?? null };
}

/** A note name with its accidentals as music signs: "Bb" → "B♭", "F##" → "F𝄪", "Bbb" → "B𝄫", "F###" → "F♯𝄪". */
function noteWithSigns(note: string): string {
    const letter = note.charAt(0), signs = note.slice(1), n = signs.length, sharp = signs.charAt(0) === '#';
    return letter + (n % 2 ? (sharp ? '♯' : '♭') : '') + (sharp ? '𝄪' : '𝄫').repeat(Math.floor(n / 2));
}

/** A chord's name with music signs, also for the altered degrees inside its quality ("m7b5" → "m7♭5"). */
export function withSigns(name: string): string {
    const parts = parseChordName(name);
    if (!parts) return name;
    const quality = parts.quality.replace(/b(?=\d)/g, '♭').replace(/#(?=\d)/g, '♯');
    return noteWithSigns(parts.root) + quality + (parts.bass ? '/' + noteWithSigns(parts.bass) : '');
}
