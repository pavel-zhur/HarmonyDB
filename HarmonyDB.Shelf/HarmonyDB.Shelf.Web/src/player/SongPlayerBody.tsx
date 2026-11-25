// The player of a song's page: with an analysis open, its own recording (the chords and bars of its views and its sheet
// are on its times — the sheet is told what sounds, and a tap on its timed chords and words plays from there);
// otherwise the recording of the song chosen to listen to. Another recording chosen while an analysis is open plays
// instead of the analysis's; a tap in the analysis (a chord, a bar, "play from here") chooses its recording again and
// goes there (analysis/clock.ts).
//
// (Loaded with the song's page by SongPlayer.tsx.)

import { useCallback, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useSongRecordings } from '../data/recordings';
import type { TrackRoute } from '../data/media';
import { chordAt } from '../analysis/piece';
import { useAnalysisPiece } from '../analysis/useAnalysisPiece';
import { sheetTime } from '../chords/time';
import { chooseAnalysis, useChosenRecording, usePlaysAnalysis } from '../screens/recordings/choice';
import { analysisPlayer, keepPlace, offerAnalysis, takePending } from '../analysis/clock';
import { ClipBox, PlayerBar } from './PlayerBar';
import { player, useClock, usePlayer, type PlayerSource } from './controller';

type SongPath = { playlistId: string; songId: string; share?: string | null };

export function SongPlayerBody({ song, analysisId, analysisShare, boundTracks = false }: { song: SongPath | null; analysisId: string | null; analysisShare?: string | null; boundTracks?: boolean }) {
    const playsAnalysis = usePlaysAnalysis(song);
    // a timed chord's "play from here", in the analysis's sheet: whichever recording plays now
    useEffect(() => (analysisId ? sheetTime.onPlayFrom(seconds => analysisPlayer.playFrom(seconds)) : undefined), [analysisId]);
    if (analysisId && (!song || playsAnalysis)) return <AnalysisPlayer chordsId={analysisId} share={analysisShare ?? null} />;
    return song ? <RecordingPlayer song={song} analysisOpen={!!analysisId} boundTracks={boundTracks} /> : null;
}

function AnalysisPlayer({ chordsId, share }: { chordsId: string; share: string | null }) {
    const { t } = useTranslation();
    const { analysis, piece } = useAnalysisPiece(chordsId, share);
    const header = analysis.data?.header;
    const source: PlayerSource | null = header && analysis.data?.content?.recording
        ? { kind: 'analysis', chordsId, share, title: header.title ?? '', subtitle: [header.artists.join(', '), t('player.analysisRecording')].filter(Boolean).join(' · ') }
        : null;

    const shown = useRef(-2);
    const started = useRef(false);
    // The sheet follows the music once it has been played: it is told when another chord sounds, and a tap on it goes
    // there (playing on, or resting there while paused). Before the first play, and after the end, the sheet is told
    // nothing: a tap on a chord sounds the chord and shows its fingerings.
    useEffect(() => {
        const stop = sheetTime.onSeek(seconds => player.seek(seconds));
        return () => {
            // another recording chosen (or the page left): where this one was, to go on from there when it comes back
            const source = player.getState().source;
            if (source?.kind === 'analysis' && source.chordsId === chordsId) keepPlace(chordsId, player.position());
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

    // a tap in the analysis made while another recording played: done once the analysis's recording is open
    const state = usePlayer();
    useEffect(() => {
        if (state.status === 'ready' && state.source?.kind === 'analysis') takePending()?.();
    }, [state.status, state.source]);

    return <PlayerBar source={source} piece={piece} />;
}

function RecordingPlayer({ song, analysisOpen, boundTracks }: { song: SongPath; analysisOpen: boolean; boundTracks: boolean }) {
    // bound: the addresses asked through the song (its link, when it came by one) — only its own recordings are signed
    const route: TrackRoute | null = !boundTracks ? null
        : song.share ? { kind: 'link', token: song.share, songId: song.songId }
        : { kind: 'song', playlistId: song.playlistId, songId: song.songId };
    const recordings = useSongRecordings(song);
    const chosen = useChosenRecording(recordings.data);
    useEffect(() => {
        if (!analysisOpen) return;
        offerAnalysis(() => chooseAnalysis(song));
        return () => offerAnalysis(null);
    }, [analysisOpen, song.playlistId, song.songId]);
    return (
        <>
            <ClipBox />
            <PlayerBar source={chosen ? { kind: 'track', track: chosen.track, route } : null} />
        </>
    );
}
