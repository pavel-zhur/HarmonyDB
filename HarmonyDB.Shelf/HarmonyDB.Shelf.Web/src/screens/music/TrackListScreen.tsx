// Lists of tracks: the chart, similar to a track, a radio (of a track, of an artist), a playlist of the service, and the
// lists of a linked account (its playlists, likes, history).

import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { useMutation, type UseQueryResult } from '@tanstack/react-query';
import { ApiError } from '../../api/client';
import { Icon } from '../../kit/Icon';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import {
    importServicePlaylist, useMusicArtistRadio, useMusicChart, useMusicPlaylist, useMusicRadio, useMusicSimilar, useMyMusicHistory, useMyMusicLikes, useMyMusicPlaylist,
    type MusicService, type MusicTrack,
} from '../../data/music';
import { AlbumTile, ArtistTile, Back, Loaded, ServiceMark, Shelf, TrackRows, type MusicPageProps } from './parts';

function TrackList({ service, title, sub, query, numbered }: {
    service: MusicService; title: string; sub?: string; query: UseQueryResult<{ tracks: MusicTrack[] }>; numbered?: boolean;
}) {
    return (
        <Page>
            <Back />
            <h1>{title} <ServiceMark service={service} /></h1>
            {sub && <p className="lead">{sub}</p>}
            <Loaded query={query}>{data => <TrackRows tracks={data.tracks} numbered={numbered} />}</Loaded>
        </Page>
    );
}

export function ChartScreen({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    const chart = useMusicChart(service);
    return <TrackList service={service} title={t('music.chart')} sub={t('music.chartSub')} query={chart} numbered />;
}

export function ArtistRadioScreen({ service, id }: MusicPageProps) {
    const { t } = useTranslation();
    const radio = useMusicArtistRadio({ service, id });
    return <TrackList service={service} title={radio.data?.title ?? t('music.artistRadio')} sub={t('music.radioSub')} query={radio} />;
}

export function RadioScreen({ service, id }: MusicPageProps) {
    const { t } = useTranslation();
    const radio = useMusicRadio({ service, id });
    return <TrackList service={service} title={radio.data?.title ?? t('music.radio')} sub={t('music.radioSub')} query={radio} />;
}

export function SimilarScreen({ service, id }: MusicPageProps) {
    const { t } = useTranslation();
    const similar = useMusicSimilar({ service, id });
    return (
        <Page>
            <Back />
            <h1>{t('music.similar')} <ServiceMark service={service} /></h1>
            <Loaded query={similar}>
                {data => (
                    <>
                        <TrackRows tracks={data.tracks} />
                        {data.albums.length > 0 && <><h2>{t('music.albums')}</h2><Shelf>{data.albums.map(album => <AlbumTile key={album.id} album={album} />)}</Shelf></>}
                        {data.artists.length > 0 && <><h2>{t('music.artists')}</h2><Shelf>{data.artists.map(artist => <ArtistTile key={artist.id} artist={artist} />)}</Shelf></>}
                    </>
                )}
            </Loaded>
        </Page>
    );
}

export function ServicePlaylistScreen({ service, id }: MusicPageProps) {
    const playlist = useMusicPlaylist({ service, id });
    return <PlaylistTracks service={service} query={playlist} />;
}

export function MyServicePlaylistScreen({ service, id }: MusicPageProps) {
    const playlist = useMyMusicPlaylist({ service, id });
    return <PlaylistTracks service={service} query={playlist} action={<ToMyPlaylists service={service} playlistId={id} />} />;
}

function PlaylistTracks({ service, query, action }: { service: MusicService; action?: ReactNode; query: UseQueryResult<{ playlist: { title?: string | null; owner?: string | null }; tracks: MusicTrack[] }> }) {
    const { t } = useTranslation();
    return (
        <Page>
            <Back />
            <h1>{query.data?.playlist.title ?? t('music.playlist')} <ServiceMark service={service} /></h1>
            {query.data?.playlist.owner && <p className="lead">{query.data.playlist.owner}</p>}
            {action}
            <Loaded query={query}>{data => <TrackRows tracks={data.tracks} />}</Loaded>
        </Page>
    );
}

export function MyLikesScreen({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    return <TrackList service={service} title={t('music.mine.likes')} query={useMyMusicLikes(service)} />;
}

export function MyHistoryScreen({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    return <TrackList service={service} title={t('music.mine.history')} query={useMyMusicHistory(service)} />;
}

/** A linked account's playlist made a playlist of the shelf; one imported before is said so, with the way to it. */
function ToMyPlaylists({ service, playlistId }: { service: MusicService; playlistId: string }) {
    const { t } = useTranslation();
    const [made, setMade] = useState<string | null>(null);
    const [before, setBefore] = useState<string | null>(null);
    const run = useMutation({
        mutationFn: importServicePlaylist,
        onSuccess: r => setMade(r.value.playlist.id),
        onError: error => { if (error instanceof ApiError && error.code === 'already_imported') setBefore(error.details?.playlistId ?? null); },
    });
    return (
        <div className="mu-mactions">
            {made
                ? <Link className="btn pri" to={`/playlists/${made}`}><Icon name="pls" />{t('music.mine.openMade')}</Link>
                : <button type="button" className="btn pri" disabled={run.isPending} onClick={() => run.mutate({ service, playlistId })}><Icon name="addpl" />{t('music.mine.toMine')}</button>}
            {before && <span className="muted small">{t('music.mine.already')} <Link to={`/playlists/${before}`}>{t('music.mine.openIt')}</Link></span>}
        </div>
    );
}