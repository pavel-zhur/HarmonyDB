// The home's music: the home is the library with music in it. Genres and moods; for a person then the artists they
// play and music like one of their playlists; the chart.
//
// Which service: the one the person linked an account of (when it is one), else Yandex Music.

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useMusicCategories, useMusicChart, useMusicHomeArtists, useMusicHomeSimilar, useMusicLinks, type MusicService } from '../../data/music';
import { useSession } from '../../data/session';
import { Icon } from '../../kit/Icon';
import { ArtistTile, CategoryTile, Shelf, TrackRows, musicHref } from './parts';

/** How many genres and moods the home shows; the rest are a tap away. */
const homeCategories = 12;

export function useHomeService(): MusicService {
    const links = useMusicLinks();
    const services = [...new Set(links.data?.map(link => link.service) ?? [])];
    return services.length === 1 ? services[0]! : 'yandex';
}

export function MusicHome() {
    const { signedIn } = useSession();
    const service = useHomeService();
    return (
        <>
            <GenresAndMoods service={service} />
            {signedIn && <ArtistsIPlay service={service} />}
            {signedIn && <SimilarToMine service={service} />}
            <Chart service={service} />
        </>
    );
}

function ArtistsIPlay({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    const artists = useMusicHomeArtists(service);
    if (!artists.data?.artists.length) return null;
    return (
        <>
            <h2>{t('music.home.artists')}</h2>
            <Shelf>{artists.data.artists.map(artist => <ArtistTile key={artist.id} artist={artist} />)}</Shelf>
        </>
    );
}

function SimilarToMine({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    const similar = useMusicHomeSimilar(service);
    const data = similar.data;
    if (!data?.tracks.length) return null;
    return (
        <>
            <h2>
                {data.playlistId
                    ? <Link className="h2l" to={`/playlists/${data.playlistId}`}>{t('music.home.similarTo', { title: data.playlistTitle ?? '' })}<Icon name="right" /></Link>
                    : t('music.home.similar')}
            </h2>
            <TrackRows tracks={data.tracks} limit={6} />
        </>
    );
}

function GenresAndMoods({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    const categories = useMusicCategories(service);
    const all = categories.data?.groups.flatMap(group => group.categories) ?? [];
    if (!all.length) return null;
    return (
        <>
            <h2><Link className="h2l" to={musicHref.categories(service)}>{t('music.genresAndMoods')}<Icon name="right" /></Link></h2>
            <div className="mu-tags">{all.slice(0, homeCategories).map(category => <CategoryTile key={category.id} service={service} category={category} />)}</div>
        </>
    );
}

function Chart({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    const chart = useMusicChart(service);
    if (!chart.data?.tracks.length) return null;
    return (
        <>
            <h2><Link className="h2l" to={musicHref.chart(service)}>{t('music.chart')}<Icon name="right" /></Link></h2>
            <TrackRows tracks={chart.data.tracks} numbered limit={5} />
        </>
    );
}
