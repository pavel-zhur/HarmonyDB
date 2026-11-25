// Recordings of songs: those a song keeps in its playlist (found automatically on both services — guesses), the one
// this person listens to, the candidates of "find another recording", adding and removing; a song made of one
// recording; the recordings found for chords alone.

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { i18n } from '../i18n';
import { action } from './actions';
import type { MusicService } from './music';

export type SongRecordings = Schemas['SongRecordings'];
export type SongRecording = Schemas['SongRecording'];

const t = i18n.t.bind(i18n);

type SongPath = { playlistId: string; songId: string; share?: string | null };

const songKey = ({ playlistId, songId, share }: SongPath) => ['recordings', playlistId, songId, share ?? null];

/** A song's recordings in its playlist, and the candidates found for its chords that are not among them. */
export function useSongRecordings(song: SongPath) {
    return useQuery({
        queryKey: songKey(song),
        queryFn: () => call(api.GET('/api/recordings/playlists/{playlistId}/songs/{songId}', {
            params: { path: { playlistId: song.playlistId, songId: song.songId }, query: { share: song.share ?? undefined } },
        })),
        staleTime: 60_000,
    });
}

/** The recordings found for chords alone (no playlist): guesses, best first. */
export function useChordsRecordings(chordsId: string, enabled = true) {
    return useQuery({
        queryKey: ['recordings', 'chords', chordsId],
        queryFn: () => call(api.GET('/api/recordings/chords/{id}', { params: { path: { id: chordsId } } })),
        staleTime: 10 * 60_000,
        enabled,
    });
}

const invalidateSong = (song: SongPath) => [['recordings', song.playlistId, song.songId], ['song', song.playlistId, song.songId], ['songs'], ['music']];

/** "Find another recording": the services are searched again for the song's chords; the answer is the song's recordings anew. */
export const findRecordings = action('findRecordings', 'change',
    (song: SongPath) => call(api.POST('/api/recordings/playlists/{playlistId}/songs/{songId}/find', {
        params: { path: { playlistId: song.playlistId, songId: song.songId }, query: { share: song.share ?? undefined } },
    })),
    { invalidate: (_, song) => [['recordings', song.playlistId, song.songId]] });

export const addRecording = action('addRecording', 'change',
    ({ song, service, trackId }: { song: SongPath; service: MusicService; trackId: string }) =>
        call(api.POST('/api/recordings/playlists/{playlistId}/songs/{songId}', {
            params: { path: { playlistId: song.playlistId, songId: song.songId } }, body: { service, trackId },
        })),
    { done: () => t('recordings.added'), undo: r => r.undo, invalidate: (_, { song }) => invalidateSong(song) });

/** Removing a recording is removing its item from the song (the playlists' area). */
export const removeRecording = action('removeRecording', 'change',
    ({ song, itemId }: { song: SongPath; itemId: string }) =>
        call(api.DELETE('/api/playlists/{id}/songs/{songId}/variants/{itemId}', {
            params: { path: { id: song.playlistId, songId: song.songId, itemId } },
        })),
    { done: () => t('recordings.removed'), undo: r => r.undo, invalidate: (_, { song }) => invalidateSong(song) });

/** The recording this person listens to in the song (null: none chosen, the first plays). */
export const chooseRecording = action('chooseRecording', 'change',
    ({ song, itemId }: { song: SongPath; itemId: string | null }) =>
        call(api.PUT('/api/recordings/playlists/{playlistId}/songs/{songId}/choice', {
            params: { path: { playlistId: song.playlistId, songId: song.songId }, query: { share: song.share ?? undefined } },
            body: { itemId },
        })),
    { invalidate: (_, { song }) => [['recordings', song.playlistId, song.songId]] });

/** A track put into a playlist as a song made of this one recording. */
export const addRecordingSong = action('addRecordingSong', 'change',
    ({ playlistId, service, trackId }: { playlistId: string; service: MusicService; trackId: string }) =>
        call(api.POST('/api/recordings/playlists/{playlistId}/songs', { params: { path: { playlistId } }, body: { service, trackId } })),
    {
        done: () => t('recordings.songAdded'), undo: r => r.undo,
        invalidate: (_, { playlistId }) => [['playlists'], ['playlist', playlistId], ['songs'], ['music']],
    });
