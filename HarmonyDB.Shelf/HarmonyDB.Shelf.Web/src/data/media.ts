// Sound and video: short-lived signed addresses (/api/media/…), played with Range requests. Every answer says when its
// addresses expire; the player asks for new ones before that (player/controller.ts).
//
// Asking is not a change: no toast, no sign-in — guests listen too.

import { api, call, type Schemas } from '../api/client';
import type { MusicService } from './music';

export type MediaAddress = Schemas['MediaAddress'];
export type ChordsMedia = Schemas['ChordsMedia'];
export type MediaStem = Schemas['MediaStem'];

/**
 * Where a track's addresses are asked: anywhere (the app), or bound to a song that lists the track among its
 * recordings (the public page, which signs only what it shows) — the song itself, or through its link.
 */
export type TrackRoute =
    | { kind: 'song'; playlistId: string; songId: string }
    | { kind: 'link'; token: string; songId: string };

/** The sound (or the video) of a track of a music service; `route`: bound to a song (above). */
export function trackMedia(service: MusicService, id: string, kind: Schemas['MediaKind'], route: TrackRoute | null = null): Promise<MediaAddress> {
    if (route?.kind === 'song') {
        return call(api.POST('/api/recordings/playlists/{playlistId}/songs/{songId}/media', {
            params: { path: { playlistId: route.playlistId, songId: route.songId } }, body: { service, id, kind },
        }));
    }
    if (route?.kind === 'link') {
        return call(api.POST('/api/share/{token}/tracks', { params: { path: { token: route.token } }, body: { songId: route.songId, service, id, kind } }));
    }
    return call(api.POST('/api/media/tracks', { body: { service, id, kind } }));
}

/**
 * The material of an analysis: the recording it was made from, its separate instruments, its notes file. Opened by a
 * share link that carries its sharer's access (`share`: its token): through the link.
 */
export function chordsMedia(chordsId: string, share?: string | null): Promise<ChordsMedia> {
    return share
        ? call(api.POST('/api/share/{token}/media', { params: { path: { token: share } } }))
        : call(api.POST('/api/media/chords', { body: { id: chordsId } }));
}
