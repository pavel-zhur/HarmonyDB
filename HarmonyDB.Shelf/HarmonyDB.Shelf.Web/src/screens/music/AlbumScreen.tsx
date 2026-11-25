// An album: its cover, title, artists, year, its tracks in order.

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Page } from '../../app/panes';
import { useMusicAlbum } from '../../data/music';
import { Back, Loaded, ServiceMark, TrackRows, musicHref, type MusicPageProps } from './parts';

/** What the album is, by the service's word for it ("music", "single", "ep"…): a word it has not, an album. */
const KINDS: Record<string, string> = { music: 'album', album: 'album', single: 'single', ep: 'ep', compilation: 'compilation', podcast: 'podcast', audiobook: 'audiobook' };

function albumKind(type: string | null | undefined, t: TFunction): string {
    return t(`music.albumKind.${KINDS[(type ?? '').toLowerCase()] ?? 'album'}` as 'music.albumKind.album');
}

export function AlbumScreen({ service, id }: MusicPageProps) {
    const { t } = useTranslation();
    const album = useMusicAlbum({ service, id });
    return (
        <Page>
            <Back />
            <Loaded query={album}>
                {({ album: a, tracks }) => (
                    <>
                        <div className="mu-mhero">
                            {a.coverUrl ? <img className="alc" src={a.coverUrl} alt="" referrerPolicy="no-referrer" /> : <span className="alc mu-blank" />}
                            <div>
                                <div className="kind">{albumKind(a.type, t)} · <ServiceMark service={service} /></div>
                                <h1>{a.title}</h1>
                                <div className="who">
                                    {a.artists.map((artist, i) => (
                                        <span key={artist.id ?? i}>
                                            {i > 0 && ', '}
                                            {artist.id ? <Link to={musicHref.artist(service, artist.id)}>{artist.name}</Link> : artist.name}
                                        </span>
                                    ))}
                                    {a.year ? ` · ${a.year}` : ''}
                                    {` · ${t('music.tracks', { count: a.trackCount ?? tracks.length })}`}
                                </div>
                            </div>
                        </div>
                        <TrackRows tracks={tracks} numbered />
                    </>
                )}
            </Loaded>
        </Page>
    );
}
