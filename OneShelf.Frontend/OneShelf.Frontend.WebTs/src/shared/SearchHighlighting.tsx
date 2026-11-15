// The original: Shared/SearchHighlighting.razor.
import { getFragments } from '../lib/common/HarmonyDB.Common.FullTextSearch/SearchHighlightingTools';
import './SearchHighlighting.scoped.scss';

export interface SearchHighlightingProps {
    query: string | null;
    text: string;
}

export function SearchHighlighting({ query, text }: SearchHighlightingProps) {
    return (
        <>
            {getFragments(query, text).map(({ fragment, isHighlighted }, i) => (
                <span key={i} className={isHighlighted ? 'search-highlighted' : undefined}>{fragment}</span>
            ))}
        </>
    );
}
