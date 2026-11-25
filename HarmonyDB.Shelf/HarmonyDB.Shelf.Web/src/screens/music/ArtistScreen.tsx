// An artist: who it is, its radio, its popular tracks, albums, singles and the rest, clips and live shows, similar artists.

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { useMusicArtist } from '../../data/music';
import { Icon } from '../../kit/Icon';
import { AlbumTile, ArtistTile, Back, Loaded, ServiceMark, Shelf, TrackRows, musicHref, type MusicPageProps } from './parts';

export function ArtistScreen({ service, id }: MusicPageProps) {
    const { t } = useTranslation();
    const artist = useMusicArtist({ service, id });
    return (
        <Page>
            <Back />
            <Loaded query={artist}>
                {page => (
                    <>
                        <div className="mu-mhero">
                            {page.artist.coverUrl ? <img className="aph" src={page.artist.coverUrl} alt="" referrerPolicy="no-referrer" /> : <span className="aph mu-blank" />}
                            <div>
                                <div className="kind">{t('music.artist')} · <ServiceMark service={service} /></div>
                                <h1>{page.artist.name}</h1>
                                {page.description && <p className="about">{page.description}</p>}
                            </div>
                        </div>
                        <div className="mu-mactions">
                            <Link className="btn" to={musicHref.artistRadio(service, id)}><Icon name="loop" />{t('music.artistRadio')}</Link>
                        </div>
                        {page.popularTracks.length > 0 && <><h2>{t('music.popular')}</h2><TrackRows tracks={page.popularTracks} limit={10} /></>}
                        {page.albums.length > 0 && <><h2>{t('music.albums')}</h2><Shelf>{page.albums.map(album => <AlbumTile key={album.id} album={album} />)}</Shelf></>}
                        {page.otherReleases.length > 0 && <><h2>{t('music.otherReleases')}</h2><Shelf>{page.otherReleases.map(album => <AlbumTile key={album.id} album={album} />)}</Shelf></>}
                        {page.videos.length > 0 && <><h2>{t('music.clips')}</h2><TrackRows tracks={page.videos} limit={10} /></>}
                        {page.similarArtists.length > 0 && <><h2>{t('music.similarArtists')}</h2><Shelf>{page.similarArtists.map(a => <ArtistTile key={a.id} artist={a} />)}</Shelf></>}
                    </>
                )}
            </Loaded>
        </Page>
    );
}
