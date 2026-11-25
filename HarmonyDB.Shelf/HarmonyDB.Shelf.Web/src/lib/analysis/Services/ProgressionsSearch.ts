import { Dictionary } from '../../common/dotnet/Collections';
import { ArgumentException, ArgumentOutOfRangeException } from '../../common/dotnet/exceptions';
import { at, atFromEnd, range, single, skipWhile, takeWhile } from '../../common/dotnet/Linq';
import type { ILogger } from '../../common/dotnet/Logging';
import { toFloat } from '../../common/dotnet/Numbers';
import { addRange } from '../../common/OneShelf.Common/Extensions';
import type { ChordsProgression } from '../Models/ChordsProgression';
import { ChordType } from '../Models/ChordType';
import type { CompactHarmonyMovement } from '../Models/CompactV1/CompactHarmonyMovement';
import type { HarmonyGroup } from '../Models/HarmonyGroup';
import type { HarmonyMovementsSequence } from '../Models/HarmonyMovementsSequence';
import type { ISearchableChordsProgression } from '../Models/Interfaces/ISearchableChordsProgression';
import type { ISearchableHarmonyMovementsSequence } from '../Models/Interfaces/ISearchableHarmonyMovementsSequence';
import { ProgressionsBuilder } from './ProgressionsBuilder';

/** (Dictionary<ChordsProgression, float> foundProgressionsWithCoverage, Dictionary<HarmonyGroup, bool> harmonyGroupsWithIsFirst) */
export interface SearchResult {
    readonly foundProgressionsWithCoverage: Map<ChordsProgression, number>;
    readonly harmonyGroupsWithIsFirst: Map<HarmonyGroup, boolean>;
}

/** (ISearchableChordsProgression progression, int index) */
interface HarmonyGroupKey {
    readonly progression: ISearchableChordsProgression;
    readonly index: number;
}

/** Dictionary.Add: throws on a duplicate key. */
function add<K, V>(dictionary: Map<K, V>, key: K, value: V): void {
    if (dictionary.has(key)) throw new ArgumentException(`An item with the same key has already been added. Key: ${String(key)}`);
    dictionary.set(key, value);
}

/**
 * The C# overloads have distinct names here: Search(IReadOnlyList<ChordsProgression>, HarmonyMovementsSequence) is
 * `search`, Search(IEnumerable<ISearchableChordsProgression>, HarmonyMovementsSequence, int?) is `searchSearchable`.
 * Coverages are floats (single precision).
 */
export class ProgressionsSearch {
    private readonly _logger: ILogger;

    constructor(logger: ILogger) {
        this._logger = logger;
    }

    extractSearch(progression: ChordsProgression, start: number, end: number): HarmonyMovementsSequence | null {
        if (start >= end) throw new ArgumentOutOfRangeException(undefined, 'start must be less than end.');

        const { harmonyGroup: harmonyGroup1, extendedMovementsSequence } = at(progression.originalSequence, start);
        const { harmonyGroup: harmonyGroup2, extendedMovementsSequence: extendedMovementsSequence2 } = at(progression.originalSequence, end);
        if (extendedMovementsSequence === null || extendedMovementsSequence2 === null || extendedMovementsSequence !== extendedMovementsSequence2) return null;

        if (harmonyGroup1 === harmonyGroup2) return null;

        const sequence = [
            ...takeWhile(skipWhile(progression.harmonySequence, x => x.harmonyGroup !== harmonyGroup1), x => x.harmonyGroup !== harmonyGroup2)
                .map(x => x.harmonyGroup),
            harmonyGroup2,
        ];

        if (sequence.length <= 1) throw new Error('Could not have happened.');

        return single(ProgressionsBuilder.getHarmonyMovementsSequences(sequence, ProgressionsBuilder.isValidExtended));
    }

    search(progressions: readonly ChordsProgression[], searchPhrase: HarmonyMovementsSequence): SearchResult {
        const { foundProgressionsWithCoverage, harmonyGroupsWithIsFirst } = this.searchInternal(progressions, searchPhrase, true);

        const resultProgressions = new Map<ChordsProgression, number>();
        for (const [key, value] of foundProgressionsWithCoverage) add(resultProgressions, key as ChordsProgression, value);

        const resultHarmonyGroups = new Map<HarmonyGroup, boolean>();
        for (const [key, value] of harmonyGroupsWithIsFirst) add(resultHarmonyGroups, at((key.progression as ChordsProgression).harmonySequence, key.index).harmonyGroup, value);

        return { foundProgressionsWithCoverage: resultProgressions, harmonyGroupsWithIsFirst: resultHarmonyGroups };
    }

    searchSearchable(progressions: Iterable<ISearchableChordsProgression>, searchPhrase: HarmonyMovementsSequence, top: number | null = 2147483647): Map<ISearchableChordsProgression, number> {
        return this.searchInternal(progressions, searchPhrase, false, top).foundProgressionsWithCoverage;
    }

    private searchInternal(
        progressions: Iterable<ISearchableChordsProgression>,
        searchPhrase: HarmonyMovementsSequence,
        harmonyGroupsToo: boolean,
        top: number | null = 2147483647,
    ): { foundProgressionsWithCoverage: Map<ISearchableChordsProgression, number>; harmonyGroupsWithIsFirst: Dictionary<HarmonyGroupKey, boolean> } {
        const result = { foundProgressionsWithCoverage: new Map<ISearchableChordsProgression, number>(), harmonyGroupsWithIsFirst: new Dictionary<HarmonyGroupKey, boolean>() };

        const isLoop = atFromEnd(searchPhrase.movements, 1).to.harmonyRepresentation === at(searchPhrase.movements, 0).from.harmonyRepresentation;
        const compactSearchPhrase = searchPhrase.compact().movements;

        for (const progression of progressions) {
            if (result.foundProgressionsWithCoverage.has(progression)) continue;

            let totalFound: Set<number> | null = null;
            let totalFoundFirsts: Set<number> | null = null;
            for (const extendedHarmonyMovementsSequence of progression.extendedHarmonyMovementsSequences) {
                const { found, foundFirsts } = this.findMatchingSubsequences(extendedHarmonyMovementsSequence, compactSearchPhrase, isLoop);
                if (found.size > 0) {
                    totalFound ??= new Set<number>();
                    totalFoundFirsts ??= new Set<number>();

                    addRange(totalFound, found);

                    if (harmonyGroupsToo) {
                        addRange(totalFoundFirsts, foundFirsts);
                    }
                }
            }

            if (totalFound !== null) {
                add(result.foundProgressionsWithCoverage, progression, toFloat(totalFound.size / toFloat(progression.harmonySequenceLength)));

                if (harmonyGroupsToo) {
                    const firsts = totalFoundFirsts!;
                    addRange(result.harmonyGroupsWithIsFirst, [...totalFound].map(i => ({ key: { progression, index: i }, value: firsts.has(i) })), false);
                }

                if (result.foundProgressionsWithCoverage.size === top)
                    break;
            }
        }

        return result;
    }

    private findMatchingSubsequences(sequence: ISearchableHarmonyMovementsSequence, searchPhrase: readonly CompactHarmonyMovement[], isLoop: boolean): { found: Set<number>; foundFirsts: Set<number>; foundFirstsFulls: Set<number> } {
        if (searchPhrase.length === 0) throw new ArgumentOutOfRangeException(undefined, "(Parameter 'searchPhrase')");

        const found = new Set<number>();
        const foundFirsts = new Set<number>();
        const foundStarts: number[] = [];
        for (let potentialStart = 0; potentialStart < sequence.movementsCount - searchPhrase.length + 1; potentialStart++) {
            let compatible = true;
            for (let j = 0; j < searchPhrase.length; j++) {
                if (!this.areCompatible(sequence.getMovement(potentialStart + j), searchPhrase[j]!)) {
                    compatible = false;
                    break;
                }
            }

            if (compatible) {
                const fromIndex = sequence.firstMovementFromIndex + potentialStart;
                addRange(found, range(fromIndex, searchPhrase.length + 1));
                foundFirsts.add(fromIndex);
                foundStarts.push(potentialStart);
            }
        }

        const foundFirstsFulls = new Set(foundFirsts);
        if (isLoop) {
            for (const start of foundStarts) {
                let lookAt = searchPhrase.length;
                while (start + lookAt < sequence.movementsCount
                    && !found.has(sequence.firstMovementFromIndex + start + lookAt + 1)
                    && this.areCompatible(sequence.getMovement(start + lookAt), searchPhrase[lookAt % searchPhrase.length]!)) {
                    found.add(sequence.firstMovementFromIndex + start + lookAt + 1);

                    if (lookAt === searchPhrase.length) {
                        foundFirsts.add(sequence.firstMovementFromIndex + start + lookAt);
                    }

                    lookAt++;
                }

                lookAt = -1;
                while (start + lookAt >= 0
                    && !found.has(sequence.firstMovementFromIndex + start + lookAt)
                    && this.areCompatible(sequence.getMovement(start + lookAt), at(searchPhrase, lookAt + searchPhrase.length))) {
                    found.add(sequence.firstMovementFromIndex + start + lookAt);
                    lookAt--;
                }
            }
        }

        return { found, foundFirsts, foundFirstsFulls };
    }

    private areCompatible(sequenceMovement: CompactHarmonyMovement, searchPhraseMovement: CompactHarmonyMovement): boolean {
        return sequenceMovement.rootDelta === searchPhraseMovement.rootDelta
            && this.areCompatibleTypes(sequenceMovement.fromType, searchPhraseMovement.fromType)
            && this.areCompatibleTypes(sequenceMovement.toType, searchPhraseMovement.toType);
    }

    private areCompatibleTypes(sequenceMovement: ChordType, searchPhraseMovement: ChordType): boolean {
        return sequenceMovement === searchPhraseMovement || searchPhraseMovement === ChordType.Unknown
            || sequenceMovement === ChordType.Unknown || (!(searchPhraseMovement === ChordType.Minor || searchPhraseMovement === ChordType.Major) && !(sequenceMovement === ChordType.Minor || sequenceMovement === ChordType.Major));
    }
}
