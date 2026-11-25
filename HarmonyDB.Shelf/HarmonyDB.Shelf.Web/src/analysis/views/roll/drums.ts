// The drums: which kind of drum a General MIDI percussion key (27–87) is, and the kinds in the order the drum lane lists
// them — cymbals on top, the kick at the bottom, as drum notation stands. Each kind is named by the view
// (analysis.roll.drum.<id>).

export type DrumKindId =
    | 'crash' | 'ride' | 'openHat' | 'closedHat' | 'pedalHat' | 'tambourine' | 'shaker' | 'cowbell'
    | 'highTom' | 'midTom' | 'snare' | 'sideStick' | 'clap' | 'lowTom' | 'kick' | 'percussion';

/** Top to bottom; `keys`: its percussion keys (the last kind takes every key no other has). */
export const DRUM_KINDS: readonly { id: DrumKindId; keys: readonly number[] }[] = [
    { id: 'crash', keys: [49, 52, 55, 57] },        // crash 1 and 2, China, splash
    { id: 'ride', keys: [51, 53, 59] },              // ride 1 and 2, its bell
    { id: 'openHat', keys: [46] },
    { id: 'closedHat', keys: [42] },
    { id: 'pedalHat', keys: [44] },
    { id: 'tambourine', keys: [54] },
    { id: 'shaker', keys: [69, 70, 82] },            // cabasa, maracas, shaker
    { id: 'cowbell', keys: [56] },
    { id: 'highTom', keys: [48, 50] },
    { id: 'midTom', keys: [45, 47] },
    { id: 'snare', keys: [38, 40] },
    { id: 'sideStick', keys: [37] },
    { id: 'clap', keys: [39] },
    { id: 'lowTom', keys: [41, 43] },
    { id: 'kick', keys: [35, 36] },
    { id: 'percussion', keys: [] },                  // bongos, congas, timbales, agogo, whistles, claves, wood blocks, triangle, …
];

const KIND_OF = new Map(DRUM_KINDS.flatMap((kind, i) => kind.keys.map(key => [key, i] as const)));
const OTHER = DRUM_KINDS.length - 1;

/** The index in DRUM_KINDS of the kind a key is. */
export function drumKind(key: number): number {
    return KIND_OF.get(key) ?? OTHER;
}
