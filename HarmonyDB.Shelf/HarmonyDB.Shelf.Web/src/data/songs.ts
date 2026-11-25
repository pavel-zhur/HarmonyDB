// Lists of songs (the library, a playlist), the one search, the recent.
//
// A list is read in pages of 200 as it is scrolled (useSongList(...).fetchNextPage); every query of a list is under
// the key ['songs', …], so a change of marks refreshes them all (local/store.ts).

import { keepPreviousData, useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { keys, readList } from '../offline';

export type SongRow = Schemas['SongRow'];
export type SongList = Schemas['SongList'];
export type SongSort = 'popular' | 'artist' | 'title' | 'newSongs' | 'newChords' | 'myMarks' | 'order';

/** Where the songs come from: the person's library, or a playlist (opened by a link: its share token). */
export type SongSource = { kind: 'library' } | { kind: 'playlist'; id: string; share?: string | null };

export interface SongListParams {
    q: string;
    sort: SongSort | null;
    /** "me", "all" or a person's id: whose marks are on the left, and (with a person's id or a mark) the filter */
    who: string | null;
    mark: string | null;
}

const pageSize = 200;

export function useSongList(source: SongSource, params: SongListParams, enabled = true) {
    const query = {
        q: params.q.trim() || undefined,
        sort: params.sort ?? undefined,
        who: params.who ?? undefined,
        mark: params.mark ?? undefined,
    };
    return useInfiniteQuery({
        queryKey: ['songs', source, query],
        // with no connection, a list of the library's copy on the device is answered from it (offline/index.ts)
        queryFn: ({ pageParam }) => source.kind === 'library'
            ? readList(keys.library, { ...query, offset: pageParam, limit: pageSize },
                () => call(api.GET('/api/library', { params: { query: { ...query, offset: pageParam, limit: pageSize } } })))
            : readList(keys.playlistRows(source.id), { ...query, offset: pageParam, limit: pageSize }, () => call(api.GET('/api/playlists/{id}/songs', {
                params: { path: { id: source.id }, query: { ...query, share: source.share ?? undefined, offset: pageParam, limit: pageSize } },
            }))),
        // asked also with no connection: then the device's copy answers
        networkMode: 'always',
        initialPageParam: 0,
        getNextPageParam: (last: SongList) => (last.offset + last.rows.length < last.total ? last.offset + last.rows.length : undefined),
        placeholderData: keepPreviousData,
        enabled,
    });
}

/** The one search: the songs of my playlists, chords from the sources (and, unit 15, the music services). */
export function useSearch(q: string, parts: ('songs' | 'chords' | 'music')[]) {
    const text = q.trim();
    return useQuery({
        queryKey: ['search', text, parts],
        queryFn: () => call(api.GET('/api/search', { params: { query: { q: text, parts: parts.join(',') } } })),
        enabled: text.length >= 2,
        placeholderData: keepPreviousData,
        // an answer some sources missed is asked again next time
        staleTime: query => (query.state.data?.chordsFailures?.length ? 0 : 60_000),
    });
}

export function useRecent(limit = 50, enabled = true) {
    return useQuery({
        queryKey: ['recent', limit],
        queryFn: () => call(api.GET('/api/library/recent', { params: { query: { limit } } })),
        enabled,
    });
}

/** The address of a row: the song in its playlist, or chords alone. */
export function rowTarget(row: SongRow): { playlistId: string; songId: string } | { chords: string } | null {
    if (row.songId && row.playlistId) return { playlistId: row.playlistId, songId: row.songId };
    if (row.chords) return { chords: row.chords.id };
    return null;
}

/** A row's identity in a list (for "the one I came back from"). */
export function rowId(row: SongRow): string {
    return row.songId ?? row.chords?.id ?? `${row.title}|${row.artists.join(',')}`;
}
