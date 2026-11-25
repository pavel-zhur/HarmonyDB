// A chord's written notes on the staff, and the accidentals a bar needs for them under a key signature. The notes are
// taken as they are written; nothing here renames one.
//
//   writtenHeads(notes) → [{ name, line, alter }], the lowest first: the chord's notes ("E2", "G#3", "F##3") at their own
//       octaves, under a treble clef with an 8 below (an octave above what sounds, as guitar music is written)
//     line    the place on the staff, in half spaces down from the top line (0 the top line F, 4 the middle line B,
//             8 the bottom line E, 10 the first ledger line below, C)
//     alter   the sharps (> 0) or flats (< 0): −1 a flat, 0 a natural, 1 a sharp, 2 a double sharp, −2 a double flat
//   keyAlters(signature) → what a key signature does to each of the seven letters, C … B
//   accidentalsOf(chords, alters) → for each chord of one bar, in order, which of its notes need their accidental
//       written: one where a note differs from what its line already means in the bar — from the key signature, or from
//       an accidental earlier in the bar; notes held over a bar line by a tie carry theirs with them.

export interface Head {
    name: string;
    line: number;
    alter: number;
}

/** What is written on the staff for a chord: its notes. */
export interface Sound {
    heads: Head[];
}

const LETTERS = 'CDEFGAB';
const TOP_LINE = 7 * 5 + 3;      // F5, counted in letters from C0

function alterOf(signs: string): number {
    return signs.charAt(0) === '#' ? signs.length : -signs.length;
}

/** A note written twice is one head. */
export function writtenHeads(notes: readonly string[]): Head[] {
    const seen = new Set<string>(), heads: Head[] = [];
    for (const text of notes) {
        const m = /^([A-G])(#*|b*)(-?\d+)$/.exec(text);
        if (!m || seen.has(text)) continue;
        seen.add(text);
        const height = 7 * Number(m[3]) + LETTERS.indexOf(m[1]!) + 7;
        heads.push({ name: m[1]! + m[2]!, line: TOP_LINE - height, alter: alterOf(m[2]!) });
    }
    return heads.sort((a, b) => b.line - a.line);
}

const FLATS = [6, 2, 5, 1, 4, 0, 3], SHARPS = [3, 0, 4, 1, 5, 2, 6];      // the letters, in the order signatures take them

export function keyAlters(signature: number): number[] {
    const alters = [0, 0, 0, 0, 0, 0, 0];
    for (let i = 0; i < Math.abs(signature); i++) {
        if (signature < 0) alters[FLATS[i]!] = -1;
        else alters[SHARPS[i]!] = 1;
    }
    return alters;
}

export function accidentalsOf(chords: { heads: Head[]; tied: boolean }[], alters: number[]): boolean[][] {
    const meant = new Map<number, number>();
    return chords.map(chord => chord.heads.map(head => {
        if (chord.tied) return false;
        const before = meant.get(head.line) ?? alters[LETTERS.indexOf(head.name.charAt(0))]!;
        meant.set(head.line, head.alter);
        return head.alter !== before;
    }));
}
