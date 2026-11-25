// Sound and video: short-lived signed addresses (/api/media/…), played with Range requests. Every answer says when its
// addresses expire; the player asks for new ones before that (player/controller.ts).
//
// Asking is not a change: no toast, no sign-in — guests listen too.

import { api, call, type Schemas } from '../api/client';
import type { MusicService } from './music';

export type MediaAddress = Schemas['MediaAddress'];
export type ChordsMedia = Schemas['ChordsMedia'];
export type MediaStem = Schemas['MediaStem'];

/** The sound (or the video) of a track of a music service. */
export function trackMedia(service: MusicService, id: string, kind: Schemas['MediaKind']): Promise<MediaAddress> {
    return call(api.POST('/api/media/tracks', { body: { service, id, kind } }));
}

/** The material of an analysis: the recording it was made from, its separate instruments, its notes file. */
export function chordsMedia(chordsId: string): Promise<ChordsMedia> {
    return call(api.POST('/api/media/chords', { body: { id: chordsId } }));
}
