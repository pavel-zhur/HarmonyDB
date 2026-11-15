import { isNullOrWhiteSpace, split, StringSplitOptions, substring, toLowerInvariant } from '../dotnet/text';
import { searchSyntaxAnySeparatorsToRemove, searchSyntaxRemoveSeparators, Separators, toSearchSyntax } from './FullTextSearchExtensions';

export function getFragments(query: string | null, text: string): { fragment: string; isHighlighted: boolean }[] {
    const result: { fragment: string; isHighlighted: boolean }[] = [];
    if (isNullOrWhiteSpace(text)) return result;
    const splitQuery = query == null
        ? null
        : split(toSearchSyntax(toLowerInvariant(query)), Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (splitQuery == null || splitQuery.length === 0) {
        result.push({ fragment: text, isHighlighted: false });
        return result;
    }

    let remaining = searchSyntaxRemoveSeparators(toSearchSyntax(toLowerInvariant(text)));
    let rendering = text;
    const anyToRemove = searchSyntaxAnySeparatorsToRemove(rendering);

    while (true) {
        // split.Select(x => (x, index: remaining.IndexOf(x, Ordinal))).Where(index > -1).OrderBy(index).FirstOrDefault()
        let first: { x: string; index: number } | null = null;
        for (const x of splitQuery) {
            const index = remaining.indexOf(x);
            if (index > -1 && (first == null || index < first.index)) first = { x, index };
        }

        if (first == null) {
            result.push({ fragment: remaining, isHighlighted: false });
            return result;
        }

        const index = first.index;
        let updatedIndex = index;

        if (updatedIndex > 0 && anyToRemove) {
            const expected = substring(remaining, 0, index);
            const renderingTests = toSearchSyntax(toLowerInvariant(rendering));
            while (true) {
                const test = searchSyntaxRemoveSeparators(substring(renderingTests, 0, updatedIndex));
                if (test === expected) break;

                updatedIndex++;
                if (updatedIndex > 100) throw new Error('Protection. Failed.');
            }
        }

        const matchLength = first.x.length;
        let updatedMatchLength = matchLength;

        if (anyToRemove) {
            const expected = substring(remaining, index, matchLength);
            const renderingTests = toSearchSyntax(toLowerInvariant(substring(rendering, updatedIndex)));
            while (true) {
                const test = searchSyntaxRemoveSeparators(substring(renderingTests, 0, updatedMatchLength));
                if (test === expected) break;

                updatedMatchLength++;
                if (updatedMatchLength > 100) throw new Error('Protection. Failed.');
            }
        }

        if (index === 0) {
            result.push({ fragment: substring(rendering, 0, updatedMatchLength), isHighlighted: true });
        } else {
            result.push({ fragment: substring(rendering, 0, updatedIndex), isHighlighted: false });
            result.push({ fragment: substring(rendering, updatedIndex, updatedMatchLength), isHighlighted: true });
        }

        remaining = substring(remaining, index + matchLength);
        rendering = substring(rendering, updatedIndex + updatedMatchLength);
        if (remaining === '') return result;
    }
}
