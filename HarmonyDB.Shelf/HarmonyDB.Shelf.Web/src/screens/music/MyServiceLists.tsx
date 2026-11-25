// A linked music account's own lists, on the playlists page: its playlists, likes and history, for each service the
// person linked; a plain invitation to link one where none is (the linking itself is the profile's).

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { useMusicLinks, useMyMusicPlaylists, type MusicService } from '../../data/music';
import { useSession } from '../../data/session';
import { Icon } from '../../kit/Icon';
import { PlaylistTile, Shelf, musicHref } from './parts';

export function MyServiceLists() {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const links = useMusicLinks();
    if (!signedIn || !links.data) return null;
    const services = [...new Set(links.data.map(link => link.service))];
    if (!services.length) {
        return (
            <div className="mu-mlink">
                <span className="muted small">{t('music.mine.invite')}</span>
                <Link className="btn sm" to="/profile">{t('music.mine.link')}</Link>
            </div>
        );
    }
    return <>{services.map(service => <ServiceLists key={service} service={service} />)}</>;
}

function ServiceLists({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    const playlists = useMyMusicPlaylists(service, true);
    return (
        <>
            <h2>{t(`music.mine.title.${service}`)}</h2>
            <div className="mu-mlinks">
                <Link className="lnk" to={musicHref.myLikes(service)}><Icon name="smile" />{t('music.mine.likes')}</Link>
                <Link className="lnk" to={musicHref.myHistory(service)}><Icon name="clock" />{t('music.mine.history')}</Link>
            </div>
            {playlists.isError && <p className="failed">{errorText(playlists.error)}</p>}
            {!!playlists.data?.playlists.length && <Shelf>{playlists.data.playlists.map(p => <PlaylistTile key={p.id} playlist={p} mine />)}</Shelf>}
        </>
    );
}
