// The player of a song's page: with an analysis open, its own recording (the chords and bars of its views and its sheet
// are on its times — the sheet is told what sounds, and a tap on its timed chords and words goes there); otherwise the
// recording of the song chosen to listen to. The player and its sound are loaded when a song's page opens
// (SongPlayerBody.tsx).
//
//   <SongPlayer song={{ playlistId, songId, share } | null} analysisId={the open variant, when it is an analysis} />
//   analysisShare={token}: the analysis opened by a share link that carries its sharer's access, read through it

import { lazy, Suspense } from 'react';

const Body = lazy(() => import('./SongPlayerBody').then(m => ({ default: m.SongPlayerBody })));

type SongPath = { playlistId: string; songId: string; share?: string | null };

export function SongPlayer(props: { song: SongPath | null; analysisId: string | null; analysisShare?: string | null }) {
    if (!props.song && !props.analysisId) return null;
    return (
        <Suspense fallback={null}>
            <Body {...props} />
        </Suspense>
    );
}
