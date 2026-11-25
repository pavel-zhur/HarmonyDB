// A key signature as the lead sheet engraves it after a treble clef: its sharps or flats in their order at their places
// on the staff. Where a key changes, the naturals of the old signature's signs the new one does not keep come first.

import { KEY_FLATS, KEY_SHARPS, type GlyphName } from './glyphs';

/** The room around the clef and the signature, in staff spaces. */
export const CLEF_LEFT = 0.4, AFTER_CLEF = 0.5, AFTER_KEY = 0.4;
const KEY_STEP_FLAT = 1.0, KEY_STEP_SHARP = 1.1;

export interface KeySign {
    glyph: GlyphName;
    /** the step on the staff, in half spaces down from its top line */
    step: number;
    /** the room it takes, in staff spaces */
    room: number;
}

export function keySigns(from: number, to: number): KeySign[] {
    const out: KeySign[] = [];
    const steps = (signature: number) => (signature < 0 ? KEY_FLATS : KEY_SHARPS);
    const kept = from * to > 0 ? Math.min(Math.abs(from), Math.abs(to)) : 0;
    for (let i = kept; i < Math.abs(from); i++) out.push({ glyph: 'natural', step: steps(from)[i]!, room: KEY_STEP_FLAT });
    for (let i = 0; i < Math.abs(to); i++) out.push({ glyph: to < 0 ? 'flat' : 'sharp', step: steps(to)[i]!, room: to < 0 ? KEY_STEP_FLAT : KEY_STEP_SHARP });
    return out;
}

export function signsWidth(signs: KeySign[]): number {
    return signs.reduce((w, s) => w + s.room, 0);
}
