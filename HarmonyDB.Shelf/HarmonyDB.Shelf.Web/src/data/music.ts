// The music services' navigation (Yandex Music, YouTube Music): artists, albums, tracks, similar, radio, the chart,
// genres and moods, the home's blocks, a linked account's own lists. Read only; what is put into a playlist from here is
// data/recordings.ts.
//
// The services are real and shared: what was read is kept a while (staleTime), and nothing is asked again on focus.

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { i18n } from '../i18n';
import { action } from './actions';
import { useSession } from './session';

export type MusicService = Schemas['MusicService'];
export type MusicTrack = Schemas['MusicTrack'];
export type MusicArtist = Schemas['MusicArtist'];
export type MusicAlbum = Schemas['MusicAlbum'];
export type MusicPlaylist = Schemas['MusicPlaylist'];
export type MusicCategory = Schemas['MusicCategory'];

export const musicServices: MusicService[] = ['yandex', 'youtube'];

/** What the services give changes slowly: kept for this long before it is asked again. */
const keep = 10 * 60_000;
const day = 24 * 60 * 60_000;

const options = { staleTime: keep, gcTime: 30 * 60_000, refetchOnWindowFocus: false, retry: false } as const;

type Path = { service: MusicService; id: string };

export function useMusicArtist({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'artist', id],
        queryFn: () => call(api.GET('/api/music/{service}/artists/{id}', { params: { path: { service, id } } })),
    });
}

export function useMusicArtistTracks({ service, id }: Path, enabled: boolean) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'artist', id, 'tracks'],
        queryFn: () => call(api.GET('/api/music/{service}/artists/{id}/tracks', { params: { path: { service, id } } })),
        enabled,
    });
}

export function useMusicArtistRadio({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'artist', id, 'radio'],
        queryFn: () => call(api.GET('/api/music/{service}/artists/{id}/radio', { params: { path: { service, id } } })),
    });
}

export function useMusicAlbum({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'album', id],
        queryFn: () => call(api.GET('/api/music/{service}/albums/{id}', { params: { path: { service, id } } })),
    });
}

export function useMusicTrack({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'track', id],
        queryFn: () => call(api.GET('/api/music/{service}/tracks/{id}', { params: { path: { service, id } } })),
    });
}

export function useMusicSimilar({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'track', id, 'similar'],
        queryFn: () => call(api.GET('/api/music/{service}/tracks/{id}/similar', { params: { path: { service, id } } })),
    });
}

export function useMusicRadio({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'track', id, 'radio'],
        queryFn: () => call(api.GET('/api/music/{service}/tracks/{id}/radio', { params: { path: { service, id } } })),
    });
}

export function useMusicLyrics({ service, id }: Path, enabled: boolean) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'track', id, 'lyrics'],
        queryFn: () => call(api.GET('/api/music/{service}/tracks/{id}/lyrics', { params: { path: { service, id } } })),
        enabled,
    });
}

export function useMusicPlaylist({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'playlist', id],
        queryFn: () => call(api.GET('/api/music/{service}/playlists/{id}', { params: { path: { service, id } } })),
    });
}

export function useMusicChart(service: MusicService) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'chart'],
        queryFn: () => call(api.GET('/api/music/{service}/chart', { params: { path: { service } } })),
    });
}

export function useMusicCategories(service: MusicService) {
    return useQuery({
        ...options,
        staleTime: 60 * 60_000,
        queryKey: ['music', service, 'categories'],
        queryFn: () => call(api.GET('/api/music/{service}/categories', { params: { path: { service } } })),
    });
}

export function useMusicCategory({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'category', id],
        queryFn: () => call(api.GET('/api/music/{service}/categories/{id}', { params: { path: { service, id } } })),
    });
}

/** "Artists you play": from the person's playlists (none for a guest). They change slowly: kept for a day. */
export function useMusicHomeArtists(service: MusicService) {
    const { signedIn } = useSession();
    return useQuery({
        ...options,
        staleTime: day,
        gcTime: day,
        queryKey: ['music', service, 'home', 'artists'],
        queryFn: () => call(api.GET('/api/music/{service}/home/artists', { params: { path: { service } } })),
        enabled: signedIn,
    });
}

/** "Similar to your playlist": one of the person's playlists, and tracks like its songs. */
export function useMusicHomeSimilar(service: MusicService) {
    const { signedIn } = useSession();
    return useQuery({
        ...options,
        queryKey: ['music', service, 'home', 'similar'],
        queryFn: () => call(api.GET('/api/music/{service}/home/similar', { params: { path: { service } } })),
        enabled: signedIn,
    });
}

/** The music accounts the person linked in the profile. */
export function useMusicLinks() {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['me', 'music-links'],
        queryFn: () => call(api.GET('/api/me/music-links')),
        enabled: signedIn,
    });
}

// ---- a linked account's own lists --------------------------------------------------------------------

export function useMyMusicPlaylists(service: MusicService, enabled: boolean) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'me', 'playlists'],
        queryFn: () => call(api.GET('/api/music/{service}/me/playlists', { params: { path: { service } } })),
        enabled,
    });
}

export function useMyMusicPlaylist({ service, id }: Path) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'me', 'playlist', id],
        queryFn: () => call(api.GET('/api/music/{service}/me/playlists/{id}', { params: { path: { service, id } } })),
    });
}

export function useMyMusicLikes(service: MusicService) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'me', 'likes'],
        queryFn: () => call(api.GET('/api/music/{service}/me/likes', { params: { path: { service } } })),
    });
}

export function useMyMusicHistory(service: MusicService) {
    return useQuery({
        ...options,
        queryKey: ['music', service, 'me', 'history'],
        queryFn: () => call(api.GET('/api/music/{service}/me/history', { params: { path: { service } } })),
    });
}

/**
 * A linked account's playlist made a playlist of the shelf (private; its songs come in a moment later, by the sync).
 * A second import of the same one is refused with `already_imported`, its details naming the playlist made before.
 */
export const importServicePlaylist = action('importServicePlaylist', 'import',
    ({ service, playlistId }: { service: MusicService; playlistId: string }) =>
        call(api.POST('/api/playlists/import', { body: { service, playlistId } })),
    { done: () => i18n.t('music.mine.imported'), undo: r => r.undo, invalidate: () => [['playlists'], ['songs']] });

/** The one search's music part: artists and recordings (three characters at least). */
export function useMusicSearch(q: string) {
    const text = q.trim();
    return useQuery({
        ...options,
        queryKey: ['search', text, ['music']],
        queryFn: () => call(api.GET('/api/search', { params: { query: { q: text, parts: 'music' } } })),
        enabled: text.length >= 3,
        staleTime: query => (query.state.data?.musicFailures?.length ? 0 : keep),
    });
}
