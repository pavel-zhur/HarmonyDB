// The music signs the lead sheet draws, from the Bravura font (SMuFL, Bravura.woff2 beside this file, its licence in
// OFL.txt). Sizes are in staff spaces, from Bravura's metadata (version 1.482): `w` is the advance, `top` and `bottom`
// the ink above and below the sign's origin. The font's em is four staff spaces.

export const MUSIC_FONT = 'Bravura';

export interface Glyph {
    ch: string;
    w: number;
    top: number;
    bottom: number;
    /** of a notehead: how far from its middle its stem begins (above it at the right edge for a stem that goes up,
     * below it at the left edge for one that goes down) */
    stem?: number;
    /** of a rhythm head: where a tie over it leaves it and comes to it, and how far over its middle it lies */
    tie?: { from: number; to: number; over: number };
}

const glyphs = {
    gClef8vb: { ch: '', w: 2.656, top: 4.392, bottom: 3.563 },     // with an 8 below: it sounds an octave lower (guitar)
    fermata: { ch: '', w: 2.42, top: 1.313, bottom: 0.063 },       // its origin at its foot
    gClef: { ch: '', w: 2.684, top: 4.392, bottom: 2.632 },        // origin on the G line
    flat: { ch: '', w: 0.904, top: 1.756, bottom: 0.7 },
    natural: { ch: '', w: 0.672, top: 1.364, bottom: 1.34 },
    sharp: { ch: '', w: 0.996, top: 1.4, bottom: 1.392 },
    doubleSharp: { ch: '', w: 1.0, top: 0.508, bottom: 0.5 },
    doubleFlat: { ch: '', w: 1.652, top: 1.748, bottom: 0.7 },
    tripleSharp: { ch: '', w: 2.052, top: 1.4, bottom: 1.392 },
    tripleFlat: { ch: '', w: 2.4, top: 1.756, bottom: 0.7 },
    slash: { ch: '', w: 2.12, top: 1, bottom: 1, stem: 1, tie: { from: 2.3, to: 1.0, over: 1.15 } },
    diamond: { ch: '', w: 2, top: 1, bottom: 1, stem: 0, tie: { from: 1.35, to: 0.65, over: 1.12 } },
    noteWhole: { ch: '', w: 1.688, top: 0.5, bottom: 0.5, stem: 0 },
    noteHalf: { ch: '', w: 1.18, top: 0.5, bottom: 0.5, stem: 0.168 },
    noteBlack: { ch: '', w: 1.18, top: 0.5, bottom: 0.5, stem: 0.168 },
    dot: { ch: '', w: 0.4, top: 0.2, bottom: 0.2 },
    // flags, by the note's value and its stem's direction; the origin is the stem's end
    flag8up: { ch: '', w: 1.056, top: 0.036, bottom: 3.24 },
    flag8down: { ch: '', w: 1.224, top: 3.232, bottom: 0.056 },
    flag16up: { ch: '', w: 1.116, top: 0.008, bottom: 3.252 },
    flag16down: { ch: '', w: 1.164, top: 3.248, bottom: 0.036 },
    flag32up: { ch: '', w: 1.044, top: 0.596, bottom: 3.248 },
    flag32down: { ch: '', w: 1.092, top: 3.248, bottom: 0.688 },
    // rests, by value: the whole rest hangs from its line, the half rest sits on its line
    rest1: { ch: '', w: 1.132, top: 0.036, bottom: 0.54 },
    rest2: { ch: '', w: 1.132, top: 0.568, bottom: 0.008 },
    rest4: { ch: '', w: 1.08, top: 1.492, bottom: 1.5 },
    rest8: { ch: '', w: 1, top: 0.696, bottom: 1.004 },
    rest16: { ch: '', w: 1.28, top: 0.716, bottom: 2 },
    rest32: { ch: '', w: 1.452, top: 1.704, bottom: 2 },
    rest64: { ch: '', w: 1.696, top: 1.72, bottom: 3.012 },
    metHalf: { ch: '', w: 1.364, top: 2.752, bottom: 0.564 },
    metQuarter: { ch: '', w: 1.328, top: 2.752, bottom: 0.564 },
    metEighth: { ch: '', w: 2.136, top: 2.784, bottom: 0.564 },
    metDot: { ch: '', w: 0.4, top: 0.2, bottom: 0.2 },
    chordFlat: { ch: '', w: 1.136, top: 3, bottom: 0.004 },
    chordSharp: { ch: '', w: 1.24, top: 3.048, bottom: 0.052 },
    chordDoubleSharp: { ch: '', w: 1.776, top: 1.796, bottom: 0 },
    chordDoubleFlat: { ch: '', w: 2.096, top: 3, bottom: 0.004 },
} satisfies Record<string, Glyph>;

export type GlyphName = keyof typeof glyphs;
export const GLYPH: Record<GlyphName, Glyph> = glyphs;

const RESTS: Record<number, GlyphName> = { 1: 'rest1', 2: 'rest2', 4: 'rest4', 8: 'rest8', 16: 'rest16', 32: 'rest32', 64: 'rest64' };
const FLAGS: Record<string, GlyphName> = {
    '8up': 'flag8up', '8down': 'flag8down', '16up': 'flag16up', '16down': 'flag16down', '32up': 'flag32up', '32down': 'flag32down',
};

/** The rest of a note value (1 a whole … 64). */
export function restGlyph(unit: number): GlyphName {
    const name = RESTS[unit];
    if (!name) throw new Error(`No rest of the value 1/${unit}.`);
    return name;
}

/** The flag of a note value (8, 16, 32) on a stem that goes up or down. */
export function flagGlyph(unit: number, up: boolean): GlyphName {
    const name = FLAGS[`${unit}${up ? 'up' : 'down'}`];
    if (!name) throw new Error(`No flag of the value 1/${unit}.`);
    return name;
}

// timeSig0 … timeSig9: the digits of a time signature, each two spaces tall around its origin; timeSigPlus between the
// groups of a sum ("2+2+3").
const DIGIT_WIDTHS = [1.88, 1.336, 1.784, 1.684, 1.88, 1.612, 1.736, 1.764, 1.744, 1.736];
const PLUS = { ch: 0xE08C, w: 2.0 };

export function timeDigits(number: string): { text: string; w: number } {
    let text = '', w = 0;
    for (const d of number) {
        if (d === '+') {
            text += String.fromCharCode(PLUS.ch);
            w += PLUS.w;
            continue;
        }
        text += String.fromCharCode(0xE080 + Number(d));
        w += DIGIT_WIDTHS[Number(d)] ?? 0;
    }
    return { text, w };
}

// tuplet0 … tuplet9: the figure at notes that share a beat in a number the meter does not count in (3 at a triplet).
// A figure stands on its origin, one and a half spaces tall.
const TUPLET_WIDTHS = [1.276, 0.984, 1.276, 1.184, 1.212, 1.268, 1.216, 1.212, 1.252, 1.216];
export const TUPLET_HEIGHT = 1.5;

export function tupletDigits(number: number): { text: string; w: number } {
    let text = '', w = 0;
    for (const d of String(number)) {
        text += String.fromCharCode(0xE880 + Number(d));
        w += TUPLET_WIDTHS[Number(d)] ?? 0;
    }
    return { text, w };
}

/** Line thicknesses and distances, in staff spaces (Bravura's engraving defaults). */
export const LINE = {
    staff: 0.13, thin: 0.16, thick: 0.5, separation: 0.4,
    stem: 0.12, ledger: 0.16, ledgerOut: 0.4, beam: 0.5, beamGap: 0.25, tieEnd: 0.1, tieMiddle: 0.22, bracket: 0.16,
};

/** Where the signs of a key signature sit on a treble staff, in half spaces down from the top line. */
export const KEY_SHARPS = [0, 3, -1, 2, 5, 1, 4];
export const KEY_FLATS = [4, 1, 5, 2, 6, 3, 7];
