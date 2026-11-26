// The person's own chords, written in the editor: one with its versions, a new one (a new song, or a variant of a song
// in its playlist; always also in the playlist "My chords" — unless, saved, they could not be put in a playlist just
// then: `placed: false`, they are in none, and the person puts them in one from the list of their chords),
// a new version, how a text will look (the preview), and the text of other chords to start from. The list of them is
// data/song.ts's useOwnChords.

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { i18n } from '../i18n';
import { action } from './actions';
import { useSession } from './session';

export type OwnChordsText = Schemas['OwnChordsText'];
export type OwnChordsDetails = Schemas['OwnChordsDetails'];

const t = i18n.t.bind(i18n);

export function useOwnChordsItem(id: string | null) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['own-chords', id],
        queryFn: () => call(api.GET('/api/own-chords/{id}', { params: { path: { id: id! } } })),
        enabled: signedIn && !!id,
        staleTime: Infinity,
    });
}

/** The text of chords open to the person, in the editor's markup: their own variant of them starts from it. */
export function useOwnChordsDraft(chordsId: string | null) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['own-chords-draft', chordsId],
        queryFn: () => call(api.GET('/api/own-chords/draft/{chordsId}', { params: { path: { chordsId: chordsId! } } })),
        enabled: signedIn && !!chordsId,
        staleTime: Infinity,
    });
}

/** How the text will look once saved: the content of chords as /api/chords/{id} gives it, the sheet only. */
export function useOwnChordsPreview(contents: string) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['own-chords-preview', contents],
        queryFn: async () => (await call(api.POST('/api/own-chords/preview', { body: { contents } }))).content,
        enabled: signedIn && contents.trim() !== '',
        staleTime: Infinity,
        placeholderData: previous => previous,
    });
}

const placed = () => [['own-chords'], ['playlists'], ['playlist'], ['songs'], ['song'], ['library'], ['places']];

export const createOwnChords = action('createOwnChords', 'change',
    (body: Schemas['CreateOwnChordsRequest']) => call(api.POST('/api/own-chords', { body })),
    // saved but in no playlist: the editor says so and leads to the list of own chords
    { done: r => (r.placed ? t('editor.saved') : null), invalidate: placed });

/** Own chords put in a playlist by hand (they were saved in none): a new song of them there. */
export const placeOwnChords = action('placeOwnChords', 'change',
    ({ playlistId, chords, title, artists }: { playlistId: string; chords: string; title: string; artists: string[] }) =>
        call(api.POST('/api/playlists/{id}/songs', { params: { path: { id: playlistId } }, body: { chords, title, artists } })),
    { done: () => t('editor.own.put'), undo: r => r.undo, invalidate: placed });

export const updateOwnChords = action('updateOwnChords', 'change',
    ({ id, text }: { id: string; text: OwnChordsText }) => call(api.PUT('/api/own-chords/{id}', { params: { path: { id } }, body: { text } })),
    { done: () => t('editor.savedVersion'), invalidate: (r: OwnChordsDetails) => [['own-chords'], ['chords', r.item.chordsId ?? null]] });
