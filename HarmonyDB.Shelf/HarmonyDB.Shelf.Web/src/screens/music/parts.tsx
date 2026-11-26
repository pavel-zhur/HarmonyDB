// The pieces of the music pages: a track's line (its cover, title, artists and album, whether it is in my playlists,
// whether the shelf made an analysis of it, its length), tiles of artists, albums, playlists, genres and moods, the way back, and the
// addresses of the pages.

import type { ReactNode } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import type { MusicAlbum, MusicArtist, MusicCategory, MusicPlaylist, MusicService, MusicTrack } from '../../data/music';
import { Icon } from '../../kit/Icon';
import './music.css';

// ---- addresses ----------------------------------------------------------------------------------------

export const musicHref = {
    artist: (service: MusicService, id: string) => `/music/${service}/artists/${encodeURIComponent(id)}`,
    artistRadio: (service: MusicService, id: string) => `/music/${service}/artists/${encodeURIComponent(id)}/radio`,
    album: (service: MusicService, id: string) => `/music/${service}/albums/${encodeURIComponent(id)}`,
    track: (service: MusicService, id: string) => `/music/${service}/tracks/${encodeURIComponent(id)}`,
    similar: (service: MusicService, id: string) => `/music/${service}/tracks/${encodeURIComponent(id)}/similar`,
    radio: (service: MusicService, id: string) => `/music/${service}/tracks/${encodeURIComponent(id)}/radio`,
    playlist: (service: MusicService, id: string) => `/music/${service}/playlists/${encodeURIComponent(id)}`,
    myPlaylist: (service: MusicService, id: string) => `/music/${service}/me/playlists/${encodeURIComponent(id)}`,
    myLikes: (service: MusicService) => `/music/${service}/me/likes`,
    myHistory: (service: MusicService) => `/music/${service}/me/history`,
    category: (service: MusicService, id: string) => `/music/${service}/categories/${encodeURIComponent(id)}`,
    categories: (service: MusicService) => `/music/${service}/categories`,
    chart: (service: MusicService) => `/music/${service}/chart`,
};

export interface MusicPageProps { service: MusicService; id: string }

/**
 * A music page by its address (/music/:service/…/:id): the page with its service and id, or, for a word that is no
 * service, a line saying so.
 */
export function MusicPage({ page: Screen }: { page: (props: MusicPageProps) => ReactNode }) {
    const { t } = useTranslation();
    const { service, id = '' } = useParams();
    if (service !== 'yandex' && service !== 'youtube') return <Page><p className="failed">{t('errors.no_such_service')}</p></Page>;
    return <Screen key={`${service}:${id}`} service={service} id={id} />;
}

// ---- small things -------------------------------------------------------------------------------------

/** m:ss */
export function clock(seconds: number): string {
    const s = Math.max(0, Math.floor(seconds));
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

export function artistNames(artists: { name?: string | null }[]): string {
    return artists.map(a => a.name).filter(Boolean).join(', ');
}

/** The service's mark: its name, shortly. */
export function ServiceMark({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    return <span className={`mu-svc ${service}`}>{t(`music.service.${service}`)}</span>;
}

/** Back where the person came from, when they came from somewhere in the app. */
export function Back() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const location = useLocation();
    if (location.key === 'default') return null;
    return <button type="button" className="mu-nback" onClick={() => navigate(-1)}><Icon name="left" />{t('music.back')}</button>;
}

/** What a query of a page has: its contents, or a line while it loads or when it failed. */
export function Loaded<T>({ query, children }: { query: { data?: T; error: unknown; isError: boolean }; children: (data: T) => ReactNode }) {
    const { t } = useTranslation();
    if (query.data !== undefined) return <>{children(query.data)}</>;
    if (query.isError) return <p className="failed">{errorText(query.error)}</p>;
    return <p className="loading">{t('common.loading')}</p>;
}

function Cover({ url, className }: { url?: string | null; className: string }) {
    return url ? <img className={className} src={url} alt="" loading="lazy" referrerPolicy="no-referrer" /> : <span className={`${className} mu-blank`} aria-hidden="true" />;
}

// ---- a track ------------------------------------------------------------------------------------------

/** A track's line: tapping it opens the track (to listen, and to put into a playlist). */
export function TrackRow({ track, n }: { track: MusicTrack; n?: number }) {
    const { t } = useTranslation();
    const sub = [artistNames(track.artists), track.version, track.album?.title].filter(Boolean).join(' · ');
    return (
        <Link className={track.available === false ? 'mu-trow off' : 'mu-trow'} to={musicHref.track(track.service, track.id)}>
            {n !== undefined && <span className="n">{n}</span>}
            <Cover url={track.coverUrl} className="tcov" />
            <span className="tx">
                <b>{track.title}</b>
                {sub && <span>{sub}</span>}
            </span>
            {track.inMySongs.length > 0 && <span className="tin" title={t('music.inMyPlaylists')} aria-label={t('music.inMyPlaylists')}><Icon name="pls" /></span>}
            {track.madeInto && <span className="tin" title={t('music.analysed')} aria-label={t('music.analysed')}><Icon name="ai" /></span>}
            {track.isVideo && <span className="tin q" title={t('music.video')} aria-label={t('music.video')}><Icon name="yt" /></span>}
            {track.durationMs ? <span className="d">{clock(track.durationMs / 1000)}</span> : null}
        </Link>
    );
}

export function TrackRows({ tracks, numbered, limit }: { tracks: readonly MusicTrack[]; numbered?: boolean; limit?: number }) {
    const { t } = useTranslation();
    const shown = limit ? tracks.slice(0, limit) : tracks;
    if (!shown.length) return <p className="empty">{t('music.nothing')}</p>;
    return <div className="mu-trows">{shown.map((track, i) => <TrackRow key={`${track.service}:${track.id}`} track={track} n={numbered ? i + 1 : undefined} />)}</div>;
}

// ---- tiles --------------------------------------------------------------------------------------------

export function ArtistTile({ artist }: { artist: MusicArtist }) {
    return (
        <Link className="tile mu-round" to={musicHref.artist(artist.service, artist.id)}>
            <Cover url={artist.coverUrl} className="mu-cover" />
            <b>{artist.name}</b>
        </Link>
    );
}

export function AlbumTile({ album }: { album: MusicAlbum }) {
    const sub = [artistNames(album.artists), album.year].filter(Boolean).join(' · ');
    return (
        <Link className="tile" to={musicHref.album(album.service, album.id)}>
            <Cover url={album.coverUrl} className="mu-cover" />
            <b>{album.title}</b>
            <span>{sub}</span>
        </Link>
    );
}

export function PlaylistTile({ playlist, mine }: { playlist: MusicPlaylist; mine?: boolean }) {
    const { t } = useTranslation();
    return (
        <Link className="tile" to={(mine ? musicHref.myPlaylist : musicHref.playlist)(playlist.service, playlist.id)}>
            <Cover url={playlist.coverUrl} className="mu-cover" />
            <b>{playlist.title}</b>
            <span>{playlist.trackCount !== null && playlist.trackCount !== undefined ? t('music.tracks', { count: playlist.trackCount }) : playlist.owner ?? ''}</span>
        </Link>
    );
}

export function Shelf({ children }: { children: ReactNode }) {
    return <div className="shelf">{children}</div>;
}

/** A genre or a mood: its name on its own colour, as the service gives it. */
export function CategoryTile({ service, category }: { service: MusicService; category: MusicCategory }) {
    return (
        <Link className="mu-tagt" to={musicHref.category(service, category.id)} style={category.color ? { ['--c' as string]: category.color } : undefined}>
            {category.title}
        </Link>
    );
}
