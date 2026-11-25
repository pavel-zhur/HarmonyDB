// The song page and its chords: reading (a song in its playlist, chords by id with their sheet, the analysis tools,
// illustrations) and every change of a song (each an action: data/actions.ts). Marks, keys and notes are the
// local-first store's (local/).

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ApiError, api, call, type Schemas } from '../api/client';
import { keys, readThrough } from '../offline';
import { i18n } from '../i18n';
import { action } from './actions';
import { useSession } from './session';

export type SongPage = Schemas['SongPage'];
export type ChordsVariant = Schemas['ChordsVariant'];
export type ChordsAnswer = Schemas['ChordsAnswer'];
export type ChordsHeader = Schemas['ChordsHeader'];
export type ChordsContent = Schemas['ChordsContentResponse'];
export type ChordsAnalysis = Schemas['ChordsAnalysis'];
export type SheetKeyValue = Schemas['SheetKey'];
export type PersonRef = Schemas['PersonRef'];

const t = i18n.t.bind(i18n);

/** A song in its playlist (opened by a link: its share token), with everything the page shows of it. */
export function useSongPage(playlistId: string, songId: string, share?: string | null) {
    return useQuery({
        queryKey: ['song', playlistId, songId, share ?? null],
        queryFn: () => readThrough(keys.song(playlistId, songId), () => call(api.GET('/api/playlists/{id}/songs/{songId}', {
            params: { path: { id: playlistId, songId }, query: { share: share ?? undefined } },
        }))),
        placeholderData: keepPreviousData,
        networkMode: 'always',
    });
}

/** Chords by id: the header, the sheet, and this person's own marks, keys and notes on them. */
export function useChordsContent(chordsId: string | null) {
    return useQuery({
        queryKey: ['chords', chordsId],
        queryFn: () => readThrough(keys.chords(chordsId!), () => call(api.GET('/api/chords/{id}', { params: { path: { id: chordsId! }, query: { kinds: 'sheet' } } }))),
        enabled: !!chordsId,
        staleTime: 5 * 60_000,
        networkMode: 'always',
    });
}

/** The detected keys with their confidence, and the key the source states. */
export function useChordsAnalysis(chordsId: string | null, enabled = true) {
    return useQuery({
        queryKey: ['chords-analysis', chordsId],
        queryFn: () => call(api.GET('/api/analysis/chords/{id}', { params: { path: { id: chordsId! } } })),
        enabled: !!chordsId && enabled,
        staleTime: Infinity,
    });
}

/** Items with the same progression ("Am F C G"), a page of them. */
/** Songs with a progression (the index's search; it may take seconds). Dropped, and its request cancelled, when no one
 * waits for it any more (the tool closed, cleared, another progression chosen). */
export function useSimilarChords(progression: string | null, page = 1) {
    return useQuery({
        queryKey: ['similar', progression, page],
        queryFn: ({ signal }) => call(api.GET('/api/analysis/similar', { params: { query: { progression: progression!, page } }, signal })),
        enabled: !!progression,
        staleTime: Infinity,
    });
}

export function useIllustrations(chordsId: string | null) {
    return useQuery({
        queryKey: ['illustrations', chordsId],
        queryFn: () => call(api.GET('/api/illustrations/{chordsId}', { params: { path: { chordsId: chordsId! } } })),
        enabled: !!chordsId,
        staleTime: 5 * 60_000,
    });
}

/**
 * Where these chords are in my playlists: my playlists (may I add to each), the songs that hold the chords in each,
 * my "here" keys for them per playlist. A guest has none.
 */
export function useChordsPlaces(chordsId: string | null) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['places', chordsId],
        queryFn: () => call(api.GET('/api/library/places', { params: { query: { chords: [chordsId!] } } })),
        enabled: signedIn && !!chordsId,
    });
}

/** My own chords (to know a variant that is mine, and edit it). */
export function useOwnChords() {
    const { signedIn } = useSession();
    return useQuery({ queryKey: ['own-chords'], queryFn: () => call(api.GET('/api/own-chords')), enabled: signedIn, staleTime: 5 * 60_000 });
}

// a song's page, the lists, and its recordings (a linked or separated song takes its recordings with it)
const songKeys = (playlistId: string) => [['song', playlistId], ['songs'], ['playlist', playlistId], ['library'], ['recordings', playlistId]];

export const setMainVariant = action('setMainVariant', 'change',
    ({ playlistId, songId, itemId }: { playlistId: string; songId: string; itemId: string | null }) =>
        call(api.PUT('/api/playlists/{id}/songs/{songId}/main', { params: { path: { id: playlistId, songId } }, body: { itemId } })),
    { done: (_, { itemId }) => t(itemId ? 'song.mainSet' : 'song.mainUnset'), undo: r => r.undo, invalidate: (_, { playlistId }) => [['song', playlistId]] });

/** The songs given become variants of this one, named as asked (one call). */
export const linkSongs = action('linkSongs', 'change',
    ({ playlistId, songId, songIds, title, artists }: { playlistId: string; songId: string; songIds: string[]; title: string; artists: string[] }) =>
        call(api.POST('/api/playlists/{id}/songs/{songId}/link', { params: { path: { id: playlistId, songId } }, body: { songIds, title, artists } })),
    { done: () => t('song.linked'), undo: r => r.undo, invalidate: (_, { playlistId }) => songKeys(playlistId) });

/** A variant goes out into a song of its own. */
export const unlinkVariant = action('unlinkVariant', 'change',
    ({ playlistId, songId, itemId }: { playlistId: string; songId: string; itemId: string }) =>
        call(api.POST('/api/playlists/{id}/songs/{songId}/variants/{itemId}/unlink', { params: { path: { id: playlistId, songId, itemId } } })),
    { done: () => t('song.unlinked'), undo: r => r.undo, invalidate: (_, { playlistId }) => songKeys(playlistId) });

export const removeVariant = action('removeVariant', 'change',
    ({ playlistId, songId, itemId }: { playlistId: string; songId: string; itemId: string }) =>
        call(api.DELETE('/api/playlists/{id}/songs/{songId}/variants/{itemId}', { params: { path: { id: playlistId, songId, itemId } } })),
    { done: () => t('song.removed'), undo: r => r.undo, invalidate: (_, { playlistId }) => songKeys(playlistId) });

/** What one sees, into one's own playlist: these chords, or (from a playlist) the song with its variants. */
export const addToPlaylist = action('addToPlaylist', 'change',
    ({ playlistId, chords, fromSongId, share }: { playlistId: string; chords?: string; fromSongId?: string; share?: string | null }) =>
        call(api.POST('/api/playlists/{id}/songs', {
            params: { path: { id: playlistId } },
            body: fromSongId ? { fromSongId, share: share ?? null } : { chords: chords! },
        })),
    { done: () => t('song.added'), undo: r => r.undo, invalidate: (_, { playlistId }) => [...songKeys(playlistId), ['song'], ['places']] });

/** The link to chords (or to a song in its playlist): the same link every time it is asked. */
export async function shareLink(body: Schemas['CreateShareRequest']): Promise<string> {
    const link = await call(api.POST('/api/share', { body }));
    return `${location.origin}/s/${link.token}`;
}

/**
 * The person's link to chords that opens on the public page and carries the person's access (whoever holds it sees
 * the chords as the person does): the one there is, made by nothing here (a guest has none).
 */
export function useStaticLink(chordsId: string | null) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['static-link', chordsId],
        queryFn: () => call(api.GET('/api/share/mine', { params: { query: { chords: chordsId! } } })),
        enabled: signedIn && !!chordsId,
    });
}

/** Makes it; chords that would not open by my keys alone (my private chords or analysis) are refused — `notOpen`,
 * said where the link would be, as is `unavailable` (the source did not answer: not checked now). */
export const makeStaticLink = action('makeStaticLink', 'change',
    async (chords: string): Promise<{ refused: 'notOpen' | 'unavailable' | null }> => {
        try {
            await call(api.POST('/api/share', { body: { kind: 'chords', chords, carriesAccess: true } }));
            return { refused: null };
        } catch (error) {
            if (error instanceof ApiError && error.code === 'chords_not_open') return { refused: 'notOpen' };
            if (error instanceof ApiError && error.code === 'unavailable') return { refused: 'unavailable' };
            throw error;
        }
    },
    { invalidate: ({ refused }, chords) => (refused ? [] : [['static-link', chords]]) });

export const removeStaticLink = action('removeStaticLink', 'change',
    ({ token }: { token: string; chords: string }) => call(api.DELETE('/api/share/{token}', { params: { path: { token } } })),
    { done: () => t('song.share.static.removed'), invalidate: (_, { chords }) => [['static-link', chords]] });
