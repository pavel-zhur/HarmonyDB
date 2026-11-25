// A playlist's songs as the public page lists them: its title, its description, "artist — title" lines, each opening
// here (`to(songId)`); read in pages as the app reads them.

import { Link } from 'react-router';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api, call } from '../api/client';
import { errorText } from '../api/errors';
import { Icon } from '../kit/Icon';

const pageSize = 200;

export function PlaylistSongs({ playlistId, share, to }: { playlistId: string; share?: string; to: (songId: string) => string }) {
    const { t } = useTranslation();
    const playlist = useQuery({
        queryKey: ['playlist', playlistId, share ?? null],
        queryFn: () => call(api.GET('/api/playlists/{id}', { params: { path: { id: playlistId }, query: { share } } })),
        staleTime: 5 * 60_000,
    });
    // read here, not through data/songs.ts: that one also keeps the app's lists on the device
    const songs = useInfiniteQuery({
        queryKey: ['songs', playlistId, share ?? null],
        queryFn: ({ pageParam }) => call(api.GET('/api/playlists/{id}/songs', {
            params: { path: { id: playlistId }, query: { share, sort: 'order', offset: pageParam, limit: pageSize } },
        })),
        initialPageParam: 0,
        getNextPageParam: last => (last.offset + last.rows.length < last.total ? last.offset + last.rows.length : undefined),
    });
    const rows = songs.data?.pages.flatMap(page => page.rows) ?? [];
    const error = playlist.error ?? songs.error;
    return (
        <>
            <h2>{playlist.data?.playlist.title ?? ' '}</h2>
            {playlist.data?.playlist.description && <p className="muted">{playlist.data.playlist.description}</p>}
            {error && <p className="failed">{errorText(error)}</p>}
            {!error && !songs.data && <p className="loading">{t('common.loading')}</p>}
            {songs.data && rows.length === 0 && <p className="muted">{t('library.empty')}</p>}
            <div className="rows">
                {rows.filter(row => row.songId).map(row => (
                    <Link key={row.songId} to={to(row.songId!)} className="row">
                        {row.cover ? <img className="rcov" src={row.cover} alt="" loading="lazy" /> : <span className="rcov pub-play"><Icon name="play" /></span>}
                        <span className="tx">
                            {row.artists.length > 0 && <>{row.artists.join(', ')} — </>}
                            <b>{row.title}</b>
                        </span>
                    </Link>
                ))}
            </div>
            {songs.hasNextPage && (
                <button type="button" className="lnk" disabled={songs.isFetchingNextPage} onClick={() => void songs.fetchNextPage()}>{t('common.showMore')}</button>
            )}
        </>
    );
}
