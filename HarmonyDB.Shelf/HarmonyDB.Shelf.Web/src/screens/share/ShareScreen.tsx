// A link (/s/:token) opens what it points at, with or without signing in: a playlist (of `link` access) — its page,
// read through the link; a song — the song in its playlist, through the link (a song of a private playlist: the song
// alone, here, without whose playlist it is); chords — the chords alone.

import { Navigate, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiError, api, call } from '../../api/client';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { SongPane } from '../song/SongPane';

export function ShareScreen() {
    const { t } = useTranslation();
    const { token } = useParams() as { token: string };
    const link = useQuery({ queryKey: ['share', token], queryFn: () => call(api.GET('/api/share/{token}', { params: { path: { token } } })) });
    const v = link.data;
    if (link.isError) return <Page><p className="failed">{errorText(link.error)}</p></Page>;
    if (!v) return <Page><p className="loading">{t('common.loading')}</p></Page>;
    const share = encodeURIComponent(token);
    if (v.kind === 'playlist' && v.playlist) return <Navigate replace to={`/playlists/${v.playlist.id}?share=${share}`} />;
    if (v.kind === 'song' && v.song?.fromPrivatePlaylist) {
        return (
            <div className="pane song">
                <SongPane target={{ playlistId: v.song.song.playlistId, songId: v.song.song.id, share: token }} closeTo="/" />
            </div>
        );
    }
    if (v.kind === 'song' && v.song) return <Navigate replace to={`/playlists/${v.song.song.playlistId}/songs/${v.song.song.id}?share=${share}`} />;
    if (v.kind === 'chords' && v.chords) return <Navigate replace to={`/chords/${encodeURIComponent(v.chords.id)}`} />;
    return <Page><p className="failed">{errorText(new ApiError(404, 'no_such_link'))}</p></Page>;
}
