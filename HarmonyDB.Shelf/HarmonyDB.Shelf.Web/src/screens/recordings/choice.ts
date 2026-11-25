// Which recording of a song plays: the one the person chose (at once in the page; for a person also kept on the server,
// where it comes from on the next visit), else the first of the song's recordings.

import { useSyncExternalStore } from 'react';
import type { SongRecordings, SongRecording } from '../../data/recordings';

const choiceHere = new Map<string, string>();
const listeners = new Set<() => void>();

export const songKey = (song: { playlistId: string; songId: string }) => `${song.playlistId}:${song.songId}`;

export function chooseHere(song: { playlistId: string; songId: string }, itemId: string): void {
    choiceHere.set(songKey(song), itemId);
    for (const listener of listeners) listener();
}

function subscribe(listener: () => void) {
    listeners.add(listener);
    return () => listeners.delete(listener);
}

/** The recording that plays, of the song's recordings as read. */
export function useChosenRecording(recordings: SongRecordings | undefined): SongRecording | null {
    const here = useSyncExternalStore(subscribe, () => (recordings ? choiceHere.get(songKey(recordings)) : undefined));
    if (!recordings?.recordings.length) return null;
    const wanted = here ?? recordings.myChoice;
    return recordings.recordings.find(r => r.itemId === wanted) ?? recordings.recordings[0]!;
}
