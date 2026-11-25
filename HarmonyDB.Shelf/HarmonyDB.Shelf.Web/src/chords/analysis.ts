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
    /** its chords from where it starts in the song: "Am F C G" */
    readonly chords: string;
}

/** The loops of a progression, the shortest first (as the old frontend listed them). */
export function loopsOf(progression: ChordsProgression): Loop[] {
    const links = indexExtractor.findStructureLinks('does not matter', progression.compact());
    return links
        .map(link => ({ link, length: deserializeLoop(link.normalized).length }))
        .sort((a, b) => a.length - b.length)
        .map(({ link }) => ({ link, chords: visualizer.getLoopChordsTitle(link) }));
}

/** The chords' marks of where a loop is in the sheet: chord index → 'search' (or 'search-first' where it starts). */
export function loopAttributes(progression: ChordsProgression, loop: Loop): ReadonlyMap<number, string> {
    const search = inputParser.parseSequence(getTitleFromBeginningNote(loop.link.normalized, 0, true));
    return visualizer.buildCustomAttributesForSearch(progression, progressionsSearch.search([progression], search));
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
