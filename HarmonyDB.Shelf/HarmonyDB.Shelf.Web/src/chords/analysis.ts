// The old frontend's analysis services (lib/analysis), made once, used as its ChordsView used them: the progression of
// a sheet, its loops and their places in the sheet, a progression between two chords, the chords of a circle of fifths.

import type { ILogger } from '../lib/common/dotnet/Logging';
import type { ChordsProgression } from '../lib/analysis/Models/ChordsProgression';
import type { StructureLink } from '../lib/analysis/Models/Structure/StructureLink';
import { deserializeLoop } from '../lib/analysis/Tools/LoopSerializationExtensions';
import { getTitleFromBeginningNote } from '../lib/analysis/Tools/TonalitiesExtensions';
import { ChordDataParser } from '../lib/analysis/Services/ChordDataParser';
import { IndexExtractor } from '../lib/analysis/Services/IndexExtractor';
import { InputParser } from '../lib/analysis/Services/InputParser';
import { PathsSearcher } from '../lib/analysis/Services/PathsSearcher';
import { ProgressionsBuilder } from '../lib/analysis/Services/ProgressionsBuilder';
import { ProgressionsSearch } from '../lib/analysis/Services/ProgressionsSearch';
import { ProgressionsVisualizer } from '../lib/analysis/Services/ProgressionsVisualizer';
import { NoteAlteration } from '../lib/common/HarmonyDB.Common/NoteAlteration';
import { Note, type RepresentationSettings } from '../lib/representations';

const logger: ILogger = {
    logInformation: () => undefined,
    logWarning: (message, ...args) => console.warn(message, ...args),
    logError: (error, message) => console.error(message, error),
};

export const chordDataParser = new ChordDataParser(logger);
const progressionsBuilder = new ProgressionsBuilder(logger);
const indexExtractor = new IndexExtractor(new PathsSearcher());
const inputParser = new InputParser(progressionsBuilder, chordDataParser);
const progressionsSearch = new ProgressionsSearch(logger);
const visualizer = new ProgressionsVisualizer();

/** The progression of the sheet's chords (their chord data, as NodeHtml.asChords gives them). */
export function progressionOf(chords: readonly string[]): ChordsProgression {
    return progressionsBuilder.buildProgression(chords.map(x => chordDataParser.getProgressionData(x)));
}

export interface Loop {
    readonly link: StructureLink;
    /** its chords where it first sounds in the song, named as the sheet shows them: "Am F C G" */
    readonly chords: string;
}

/**
 * The sheet's chords (as NodeHtml.asChords gives them: "[!5b!]m7/[!0!]", the notes by their tones) named as the sheet
 * shows them under these settings: its spelling, its transposition (a moved note takes the chosen ♭ or ♯, else ♯, as
 * the sheet's does).
 */
export function chordNames(chords: readonly string[], settings: RepresentationSettings): string[] {
    const written = settings.with({ transpose: 0, relativeTo: null });
    return chords.map(chord => chord.replace(/\[!(\d+)([b#]?)!\]/g, (_, tone: string, sign: string) => {
        const alteration = settings.transpose !== 0 ? null : sign === 'b' ? NoteAlteration.Flat : sign === '#' ? NoteAlteration.Sharp : null;
        return new Note(Number(tone), alteration).representation(written);
    }));
}

/**
 * The loops of a progression, the shortest first (as the old frontend listed them). Each is named by the sheet's own
 * chords where it first sounds (`names`: the sheet's chords as shown, the progression's source), so it speaks the
 * sheet's spelling, key and simplification — the old frontend named them from the loop's notes, in sharps, with "?"
 * for a chord it could not type.
 */
export function loopsOf(progression: ChordsProgression, names: readonly string[]): Loop[] {
    const links = indexExtractor.findStructureLinks('does not matter', progression.compact());
    return links
        .map(link => ({ link, length: deserializeLoop(link.normalized).length }))
        .sort((a, b) => a.length - b.length)
        .map(({ link }) => ({ link, chords: firstSounding(progression, link, names) }));
}

/** The sheet's chords of a loop's first occurrence: from its first chord up to where it starts again or stops. */
function firstSounding(progression: ChordsProgression, link: StructureLink, names: readonly string[]): string {
    const attributes = placesOf(progression, link);
    const indices = [...attributes.keys()].sort((a, b) => a - b);
    const start = indices.findIndex(i => attributes.get(i) === ProgressionsVisualizer.AttributeSearchFirst);
    const taken: string[] = [];
    for (let k = start; k < indices.length; k++) {
        const i = indices[k]!;
        if (k > start && (i !== indices[k - 1]! + 1 || attributes.get(i) === ProgressionsVisualizer.AttributeSearchFirst)) break;
        const name = names[i];
        if (name !== undefined && name !== taken[taken.length - 1]) taken.push(name);
    }
    // the loop was found in this progression, so it sounds in it at least once
    return taken.join(' ');
}

function placesOf(progression: ChordsProgression, link: StructureLink): ReadonlyMap<number, string> {
    const search = inputParser.parseSequence(getTitleFromBeginningNote(link.normalized, 0, true));
    return visualizer.buildCustomAttributesForSearch(progression, progressionsSearch.search([progression], search));
}

/** The chords' marks of where a loop is in the sheet: chord index → 'search' (or 'search-first' where it starts). */
export function loopAttributes(progression: ChordsProgression, loop: Loop): ReadonlyMap<number, string> {
    return placesOf(progression, loop.link);
}

/** The first chord of a progression to find, marked. */
export function firstAttribute(index: number): ReadonlyMap<number, string> {
    return new Map([[index, ProgressionsVisualizer.AttributeSearchFirst]]);
}

/** The progression from one chord to another, as chord names for the similar songs' search ("Am F C G"); null: none. */
export function progressionBetween(progression: ChordsProgression, start: number, end: number): { names: string; attributes: ReadonlyMap<number, string> } | null {
    const search = progressionsSearch.extractSearch(progression, start, end);
    if (search === null) return null;
    const attributes = visualizer.buildCustomAttributesForSearch(progression, progressionsSearch.search([progression], search));
    // a chord held over several places is one chord of the progression
    const groups = progression.originalSequence.slice(start, end + 1).map(x => x.harmonyGroup);
    const names = groups.filter((x, i) => i === 0 || groups[i - 1] !== x).map(x => x.harmonyRepresentation);
    return { names: names.join(' '), attributes };
}
