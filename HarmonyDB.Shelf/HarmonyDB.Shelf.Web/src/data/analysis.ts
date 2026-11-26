// An analysis of a recording, as an item of chords has it: the typed kinds of its content (the source contract's
// version 2) — its time line (keys, bars and beats, chords, sections), words with times, notes, a score, charts, and the
// recording with its separate instruments. The sheet (text with chords) is the song page's.

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { addressOf } from './address';

export type Timeline = Schemas['SourceV2Timeline'];
export type TimelineBar = Schemas['SourceV2Bar'];
export type TimelineChord = Schemas['SourceV2TimelineChord'];
export type TimelineSection = Schemas['SourceV2Section'];
export type Lyrics = Schemas['SourceV2Lyrics'];
export type Notes = Schemas['SourceV2Notes'];
export type Score = Schemas['SourceV2Score'];
export type Chart = Schemas['SourceV2Chart'];
export type Recording = Schemas['SourceV2Recording'];
export type ChordsKind = Schemas['ChordsKind'];

/** The kinds the views of an analysis read. */
export const analysisKinds: ChordsKind[] = ['timeline', 'lyrics', 'notes', 'score', 'chart', 'recording'];

/** Whether an item of chords is an analysis: it has a time line. */
export function isAnalysis(kinds: readonly ChordsKind[] | null | undefined): boolean {
    return !!kinds?.includes('timeline');
}

/** The analysis, read by its id — or, opened by a share link (`share`: its token), through the link. */
export function useAnalysis(chordsId: string | null, share?: string | null) {
    return useQuery({
        queryKey: ['analysis', chordsId, share ?? null],
        queryFn: () => (share
            ? call(api.GET('/api/share/{token}/chords', { params: { path: { token: share }, query: { kinds: analysisKinds.join(',') } } }))
            : call(api.GET('/api/chords/{id}', { params: { path: { id: chordsId! }, query: { kinds: analysisKinds.join(','), address: addressOf(chordsId!) } } }))),
        enabled: !!chordsId,
        // an analysis does not change while it is open
        staleTime: Infinity,
    });
}
