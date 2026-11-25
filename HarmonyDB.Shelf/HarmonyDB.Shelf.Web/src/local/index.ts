// The local-first small changes — the one surface the screens use:
//
//   local.mark(chordsId, 'heart_red', true)          // put (false: take off) my mark
//   local.key(chordsId, 2)                           // my key ("mine"); null forgets it
//   local.key(chordsId, 5, playlistId)               // my key "here", in a playlist
//   local.note({ chords: chordsId }, 'text')         // my personal note; null or '' removes it
//   local.note({ songId }, 'text')                   // my note in the song's playlist
//   local.noteTyped({ chords: chordsId }, 'text')    // the same, kept as it is typed: no toast
//   local.opened({ chords: chordsId, songId })       // opened (for "Recent"; no toast, no undo)
//
//   const marks = useMyMarks(chordsId)               // what I see now: the server's word + what is not sent yet
//   myMarksOn(useLocalView(), row.chordsIds)         // the same over a song's variants (a list's row)
//   const key = useMyKey(chordsId, playlistId?)
//   const note = useMyNote({ chords: chordsId })
//   const { busy, queued } = useSyncStatus()         // the header's sync button spins while busy
//   await local.exchange()                           // send now and take what changed (the sync button)
//   local.forgetUnsent()                             // the changes not sent yet given up (signing out, by choice)
//
// Each change but "opened" shows a toast with "Undo". A guest's change offers signing in and is made after it.

import { useMemo, useSyncExternalStore } from 'react';
import { enqueue, exchange, forgetUnsent, getStatus, getView, subscribe, toastFor } from './store';
import { keyKey, markKey, noteKey } from './state';

function change(c: Parameters<typeof enqueue>[0]): void {
    const id = enqueue(c);
    if (id !== null) toastFor(c, id);
}

export const local = {
    mark: (chords: string, kind: string, on: boolean) => change({ type: 'mark', chords, kind, on }),
    key: (chords: string, semitones: number | null, playlistId?: string) => change({ type: 'key', chords, semitones, playlistId: playlistId ?? null }),
    note: (target: { chords: string } | { songId: string }, text: string | null) =>
        change({ type: 'note', chords: 'chords' in target ? target.chords : null, songId: 'songId' in target ? target.songId : null, text }),
    /** a note kept as it is typed: no toast (the field's own undo is the undo of typing) */
    noteTyped: (target: { chords: string } | { songId: string }, text: string | null) => {
        enqueue({ type: 'note', chords: 'chords' in target ? target.chords : null, songId: 'songId' in target ? target.songId : null, text });
    },
    opened: (target: { chords?: string | null; songId?: string | null }) => {
        enqueue({ type: 'opened', chords: target.chords ?? null, songId: target.songId ?? null });
    },
    exchange,
    /** the person's changes not sent yet, given up (they chose so, signing out) */
    forgetUnsent,
    /** how many changes wait to be sent now */
    pendingCount: () => getStatus().queued,
};

export function useLocalView() {
    return useSyncExternalStore(subscribe, getView);
}

/** My marks on the chords, the latest first. */
export function useMyMarks(chords: string): string[] {
    const view = useLocalView();
    return useMemo(() => {
        const prefix = markKey(chords, '');
        return Object.entries(view.marks).filter(([k]) => k.startsWith(prefix)).map(([, m]) => m).sort((a, b) => (a.at < b.at ? 1 : -1)).map(m => m.kind);
    }, [view, chords]);
}

/**
 * My marks on any of these chords (a song's variants), the latest first, as I see them now — once the store holds the
 * server's word (null before: then the list's own answer stands).
 */
export function myMarksOn(view: ReturnType<typeof getView>, chordsIds: readonly string[]): string[] | null {
    if (view.watermark === null) return null;
    const ids = new Set(chordsIds);
    const marks = Object.values(view.marks).filter(m => ids.has(m.chords)).sort((a, b) => (a.at < b.at ? 1 : -1)).map(m => m.kind);
    return [...new Set(marks)];
}

export function useMyKey(chords: string, playlistId?: string): number | null {
    const view = useLocalView();
    return view.keys[keyKey(chords, playlistId)]?.semitones ?? null;
}

export function useMyNote(target: { chords: string } | { songId: string }): string | null {
    const view = useLocalView();
    const k = 'chords' in target ? noteKey(target.chords, null) : noteKey(null, target.songId);
    return view.notes[k]?.text ?? null;
}

export function useSyncStatus(): { busy: boolean; queued: number } {
    return useSyncExternalStore(subscribe, getStatus);
}

export { attach, installTriggers } from './store';
