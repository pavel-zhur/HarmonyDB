// The player of a song's page: with an analysis open, its own recording (the chords and bars of its views and its sheet
// are on its times — the sheet is told what sounds, and a tap on its timed chords and words plays from there);
// otherwise the recording of the song chosen to listen to.
//
// (Loaded with the song's page by SongPlayer.tsx.)

import { useCallback, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useSongRecordings } from '../data/recordings';
import { chordAt } from '../analysis/piece';
import { useAnalysisPiece } from '../analysis/useAnalysisPiece';
import { sheetTime } from '../chords/time';
import { useChosenRecording } from '../screens/recordings/choice';
import { ClipBox, PlayerBar } from './PlayerBar';
import { player, useClock, type PlayerSource } from './controller';

type SongPath = { playlistId: string; songId: string; share?: string | null };

export function SongPlayerBody({ song, analysisId }: { song: SongPath | null; analysisId: string | null }) {
    return analysisId ? <AnalysisPlayer chordsId={analysisId} /> : song ? <RecordingPlayer song={song} /> : null;
}

function AnalysisPlayer({ chordsId }: { chordsId: string }) {
    const { t } = useTranslation();
    const { analysis, piece } = useAnalysisPiece(chordsId);
    const header = analysis.data?.header;
    const source: PlayerSource | null = header && analysis.data?.content?.recording
        ? { kind: 'analysis', chordsId, title: header.title ?? '', subtitle: [header.artists.join(', '), t('player.analysisRecording')].filter(Boolean).join(' · ') }
        : null;

    const shown = useRef(-2);
    const started = useRef(false);
    // The sheet follows the music once it has been played: it is told when another chord sounds, and a tap on it goes
    // there (playing on, or resting there while paused). Before the first play, and after the end, the sheet is told
    // nothing: a tap on a chord sounds the chord and shows its fingerings.
    useEffect(() => {
        const stop = sheetTime.onSeek(seconds => player.seek(seconds));
        return () => {
            stop();
            sheetTime.show(null);
            started.current = false;
            shown.current = -2;
        };
    }, [chordsId]);
    const follow = useCallback((position: number, playing: boolean) => {
        if (!piece || player.getState().status !== 'ready') return;
        if (playing) started.current = true;
        else if (started.current && position >= player.getState().duration - 0.05) {
            started.current = false;
            shown.current = -2;
            sheetTime.show(null);
        }
        if (!started.current) return;
        const chord = chordAt(piece, position);
        if (chord === shown.current) return;
        shown.current = chord;
        sheetTime.show(position);
    }, [piece]);
    useClock(follow);

    return <PlayerBar source={source} piece={piece} />;
}

function RecordingPlayer({ song }: { song: SongPath }) {
    const recordings = useSongRecordings(song);
    const chosen = useChosenRecording(recordings.data);
    return (
        <>
            <ClipBox />
            <PlayerBar source={chosen ? { kind: 'track', track: chosen.track } : null} />
        </>
    );
}
