// A list's state lives in its address (?q=…&sort=…&who=…&mark=…): a link or the back button brings the same list.

import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';
import type { SongListParams, SongSort } from '../../data/songs';

/** "compact": by artist, the titles of one artist on one line. */
export type ListSort = SongSort | 'compact';

export interface ListState {
    q: string;
    sort: ListSort | null;
    /** whose marks: "me" (default), "all", or a person's id */
    who: string;
    mark: string | null;
}

const sorts: ListSort[] = ['popular', 'artist', 'title', 'newSongs', 'newChords', 'myMarks', 'order', 'compact'];

export function useListState(): [ListState, (patch: Partial<ListState>) => void, string] {
    const [params, setParams] = useSearchParams();
    const state = useMemo<ListState>(() => {
        const sort = params.get('sort') as ListSort | null;
        return {
            q: params.get('q') ?? '',
            sort: sort && sorts.includes(sort) ? sort : null,
            who: params.get('who') ?? 'me',
            mark: params.get('mark'),
        };
    }, [params]);

    const update = useCallback((patch: Partial<ListState>) => {
        setParams(previous => {
            const next = new URLSearchParams(previous);
            for (const [key, value] of Object.entries(patch)) {
                if (value === null || value === '' || (key === 'who' && value === 'me')) next.delete(key);
                else next.set(key, String(value));
            }
            return next;
        }, { replace: true });
    }, [setParams]);

    return [state, update, params.toString()];
}

/** What the server is asked: its sort, and the filter by person and mark. */
export function serverParams(state: ListState): SongListParams {
    const who = state.who === 'me' ? (state.mark ? 'me' : null) : state.who === 'all' ? null : state.who;
    return { q: state.q, sort: state.sort === 'compact' ? 'artist' : state.sort, who, mark: state.mark };
}
