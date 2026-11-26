// What the public page opens: chords — an analysis with the app's views of it (the text with chords first) and the
// player of its recording, the views following the music; other chords as their text with chords. A song shows its
// chords (a song of recordings only, them and their player); a playlist (by a link) its songs.
//
//   /songs/:songId           a song of the showcase (the open playlist of analyses)
//   /chords/:chordsId        chords by their id (an analysis: open to all)
//   /s/:token                a share link of the app: chords, a song or a playlist
//   /s/:token/:songId        a song of a playlist shared by a link

import { useEffect, useMemo } from 'react';
import { Navigate, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiError, api, call, type Schemas } from '../api/client';
import { errorText } from '../api/errors';
import { isAnalysis } from '../data/analysis';
import { addressOf, cameBy } from '../data/address';
import { RepresentationSettings, SimplificationMode } from '../lib/representations';
import { sheetFromApi } from '../chords/sheet';
import { ChordSheet } from '../chords/ChordSheet';
import { ChordsViewProvider } from '../chords/view';
import { AnalysisViews } from '../analysis/AnalysisViews';
import { SongPlayer } from '../player/SongPlayer';
import { ToTop } from '../app/ToTop';
import { Icon } from '../kit/Icon';
import { Invite } from './PublicFrame';
import { PlaylistSongs } from './Songs';
import { SongRecordings } from '../screens/recordings/SongRecordings';
import { appAddress, useShowcase } from './site';
import '../screens/song/song.css';

type SongPage = Schemas['SongPage'];

export function ChordsPage() {
    const { chordsId } = useParams() as { chordsId: string };
    // this page is the item's own link: its address is passed on (data/address.ts), to the app too
    const address = location.origin + location.pathname + location.search;
    cameBy(chordsId, address);
    return <Chords chordsId={chordsId} inApp={`/chords/${encodeURIComponent(chordsId)}?address=${encodeURIComponent(address)}`} />;
}

export function ShowcaseSongPage() {
    const { t } = useTranslation();
    const { songId } = useParams() as { songId: string };
    const showcase = useShowcase();
    if (showcase.isError) return <Message><p className="failed">{errorText(showcase.error)}</p></Message>;
    if (showcase.data === undefined) return <Message><p className="loading">{t('common.loading')}</p></Message>;
    if (showcase.data === null) return <Navigate to="/" replace />;
    return <SongInPlaylist playlistId={showcase.data} songId={songId} />;
}

export function SharedSongPage() {
    const { t } = useTranslation();
    const { token, songId } = useParams() as { token: string; songId: string };
    const link = useShare(token);
    if (link.isError) return <Message><p className="failed">{errorText(link.error)}</p></Message>;
    if (!link.data) return <Message><p className="loading">{t('common.loading')}</p></Message>;
    if (link.data.kind !== 'playlist' || !link.data.playlist) return <Navigate to={`/s/${encodeURIComponent(token)}`} replace />;
    return <SongInPlaylist playlistId={link.data.playlist.id} songId={songId} share={token} />;
}

export function SharePage() {
    const { t } = useTranslation();
    const { token } = useParams() as { token: string };
    const link = useShare(token);
    const inApp = `/s/${encodeURIComponent(token)}`;
    const v = link.data;
    if (link.isError) return <Message><p className="failed">{errorText(link.error)}</p></Message>;
    if (!v) return <Message><p className="loading">{t('common.loading')}</p></Message>;
    // chords by a link: read through it, as the one who shared them saw them
    if (v.kind === 'chords' && v.chords) return <Chords chordsId={v.chords.id} share={token} inApp={inApp} />;
    if (v.kind === 'song' && v.song) return <Song page={v.song} inApp={inApp} share={token} />;
    if (v.kind === 'playlist' && v.playlist) {
        return (
            <Message>
                <PlaylistSongs playlistId={v.playlist.id} share={token} to={songId => `${inApp}/${songId}`} />
                <Invite to={appAddress(inApp)} />
            </Message>
        );
    }
    return <Message><p className="failed">{errorText(new ApiError(404, 'no_such_link'))}</p></Message>;
}

function useShare(token: string) {
    return useQuery({ queryKey: ['share', token], queryFn: () => call(api.GET('/api/share/{token}', { params: { path: { token } } })) });
}

function SongInPlaylist({ playlistId, songId, share }: { playlistId: string; songId: string; share?: string }) {
    const { t } = useTranslation();
    const song = useQuery({
        queryKey: ['song', playlistId, songId, share ?? null],
        queryFn: () => call(api.GET('/api/playlists/{id}/songs/{songId}', { params: { path: { id: playlistId, songId }, query: { share } } })),
    });
    if (song.isError) return <Message><p className="failed">{errorText(song.error)}</p></Message>;
    if (!song.data) return <Message><p className="loading">{t('common.loading')}</p></Message>;
    const inApp = `/playlists/${playlistId}/songs/${songId}${share ? `?share=${encodeURIComponent(share)}` : ''}`;
    return <Song page={song.data} inApp={inApp} share={share} />;
}

/** A song: its chords that open (an analysis first), under the song's own title; a song of recordings only, them. */
function Song({ page, inApp, share }: { page: SongPage; inApp: string; share?: string }) {
    const { t } = useTranslation();
    const open = page.variants.filter(x => x.chords.state === 'open');
    const variant = open.find(x => isAnalysis(x.chords.header?.kinds)) ?? open[0] ?? page.variants[0];
    if (!variant && page.recordings.length) return <RecordingsSong page={page} inApp={inApp} share={share} />;
    if (!variant) return <Message><Title title={page.song.title} artists={page.song.artists.join(', ')} /><p className="muted">{t('song.chords.noSheet')}</p></Message>;
    return <Chords chordsId={variant.chords.id} inApp={inApp} title={page.song.title} artists={page.song.artists} />;
}

/** Chords: by their id, or (`share`: a chords link's token) through the link. */
function Chords({ chordsId, share, inApp, title, artists }: { chordsId: string; share?: string; inApp: string; title?: string; artists?: readonly string[] }) {
    const { t } = useTranslation();
    const content = useQuery({
        queryKey: ['chords', chordsId, share ?? null],
        queryFn: () => (share
            ? call(api.GET('/api/share/{token}/chords', { params: { path: { token: share }, query: { kinds: 'sheet' } } }))
            : call(api.GET('/api/chords/{id}', { params: { path: { id: chordsId }, query: { kinds: 'sheet', address: addressOf(chordsId) } } }))),
        staleTime: 5 * 60_000,
    });
    const header = content.data?.header;
    const sheet = useMemo(() => (content.data?.content?.sheet ? sheetFromApi(content.data.content.sheet.root) : null), [content.data]);
    // the chords as written, with their degrees and small signs: the app's defaults
    const settings = useMemo(() => new RepresentationSettings(
        { transpose: 0, alteration: null, simplification: SimplificationMode.None, isVariableWidth: sheet?.isVariableWidth ?? false },
        { showDegrees: true, chordSuperscripts: true, showNotesAsWritten: true }), [sheet]);

    const shownTitle = title ?? header?.title ?? '';
    const shownArtists = (artists ?? header?.artists ?? []).join(', ');
    useEffect(() => {
        if (shownTitle) document.title = [shownArtists, shownTitle].filter(Boolean).join(' — ');
    }, [shownTitle, shownArtists]);

    if (content.isError) return <Message><p className="failed">{errorText(content.error)}</p></Message>;
    if (!content.data) return <Message><p className="loading">{t('common.loading')}</p></Message>;
    const state = content.data.state;
    // an analysis shows its views whether or not it has a text with chords (a piece without words has none)
    const analysis = isAnalysis(header?.kinds);
    if (state !== 'open' || (!sheet && !analysis)) {
        return (
            <Message>
                <Title title={shownTitle} artists={shownArtists} />
                <p className="muted">{t(state === 'notOpen' ? 'public.notOpen' : state === 'unavailable' ? 'song.chords.unavailable' : 'song.chords.noSheet')}</p>
                <Invite to={appAddress(inApp)} />
            </Message>
        );
    }

    const sheetBox = sheet && <div className="sheetbox"><ChordSheet sheet={sheet} settings={settings} /></div>;
    return (
        <div className="pane song">
            <div className="songpage">
                <div className="hero">
                    <div className="htop">
                        <div className="t">
                            <div className="a">{shownArtists}</div>
                            <div className="n">{shownTitle}</div>
                            {analysis && <div className="ctx"><Icon name="ai" />{t('public.analysis')}</div>}
                        </div>
                    </div>
                </div>
                <ChordsViewProvider value={settings}>
                    {analysis ? <AnalysisViews chordsId={chordsId} share={share} sheet={sheetBox} /> : sheetBox}
                </ChordsViewProvider>
                <div className="pub-gutter"><Invite to={appAddress(inApp)} /></div>
                <div className="songfoot">
                    <ToTop />
                    {analysis && <SongPlayer song={null} analysisId={chordsId} analysisShare={share} />}
                </div>
            </div>
        </div>
    );
}

/** A song that holds only recordings: them, to listen to, with the player. */
function RecordingsSong({ page, inApp, share }: { page: SongPage; inApp: string; share?: string }) {
    const song = { playlistId: page.song.playlistId, songId: page.song.id, share: share ?? null };
    const artists = page.song.artists.join(', ');
    useEffect(() => {
        document.title = [artists, page.song.title].filter(Boolean).join(' — ');
    }, [artists, page.song.title]);
    return (
        <div className="pane song">
            <div className="songpage">
                <div className="hero">
                    <div className="htop">
                        <div className="t">
                            <div className="a">{artists}</div>
                            <div className="n">{page.song.title}</div>
                        </div>
                    </div>
                    <SongRecordings song={song} analysed readOnly />
                </div>
                <div className="pub-gutter"><Invite to={appAddress(inApp)} /></div>
                <div className="songfoot">
                    <ToTop />
                    <SongPlayer song={song} analysisId={null} boundTracks />
                </div>
            </div>
        </div>
    );
}

function Title({ title, artists }: { title: string; artists: string }) {
    if (!title) return null;
    return <p className="pub-what">{artists && <span className="muted">{artists} — </span>}<b>{title}</b></p>;
}

function Message({ children }: { children: React.ReactNode }) {
    return <div className="pane"><div className="pad">{children}</div></div>;
}
