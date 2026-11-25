// Which recording of a song plays: the one the person chose (at once in the page; for a person also kept on the server,
// where it comes from on the next visit), else the first of the song's recordings. With an analysis open, its own
// recording plays — until another is chosen in the page; a tap in the analysis (or on its recording) chooses it again.

import { useSyncExternalStore } from 'react';
import type { SongRecordings, SongRecording } from '../../data/recordings';

const choiceHere = new Map<string, string>();
/** per song: false — a recording was chosen over the open analysis's own */
const analysisHere = new Map<string, boolean>();
const listeners = new Set<() => void>();

export const songKey = (song: { playlistId: string; songId: string }) => `${song.playlistId}:${song.songId}`;

export function chooseHere(song: { playlistId: string; songId: string }, itemId: string): void {
    choiceHere.set(songKey(song), itemId);
    analysisHere.set(songKey(song), false);
    for (const listener of listeners) listener();
}

/** The open analysis's recording is what plays (again). */
export function chooseAnalysis(song: { playlistId: string; songId: string }): void {
    analysisHere.set(songKey(song), true);
    for (const listener of listeners) listener();
}

/** Whether the open analysis's recording plays rather than one of the song's recordings. */
export function usePlaysAnalysis(song: { playlistId: string; songId: string } | null): boolean {
    return useSyncExternalStore(subscribe, () => (song ? analysisHere.get(songKey(song)) ?? true : true));
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
