// The home. A person: the search (results under it as one types — the library's list and the one search), then
// "Continue" (opened lately) and "My playlists". A guest: the inviting home (GuestHome). Under them, for both, the music
// (screens/music/MusicHome: artists I play, similar, genres and moods, the chart).

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { ToTop } from '../../app/ToTop';
import { useSession } from '../../data/session';
import { playlistTitle, useMyPlaylists } from '../../data/playlists';
import { useRecent } from '../../data/songs';
import { Icon } from '../../kit/Icon';
import { SongList } from '../lists/SongList';
import { useListState } from '../lists/listParams';
import { RecentList } from '../recent/RecentScreen';
import { Cover } from '../playlists/PlaylistMeta';
import { GuestHome } from './GuestHome';
import { MusicHome } from '../music/MusicHome';

export function HomeScreen() {
    const { t } = useTranslation();
    const { known, signedIn } = useSession();
    const [state] = useListState();
    const searching = state.q.trim().length > 0;
    return (
        <div className="pane">
            <SongList source={{ kind: 'library' }} listKey="home" openId={null} sorts={['popular']}
                songHref={(p, s) => `/library/songs/${p}/${s}`} chordsHref={id => (signedIn ? `/library/chords/${id}` : `/chords/${id}`)}
                searchOnly={{ placeholder: t('home.searchPlaceholder') }}>
                {known && !searching && (signedIn ? <PersonHome /> : <GuestHome />)}
                {known && !searching && <MusicHome />}
            </SongList>
            <ToTop />
        </div>
    );
}

function PersonHome() {
    const { t } = useTranslation();
    const recent = useRecent(4);
    const playlists = useMyPlaylists();
    // the playlists in my library; the system ones once they hold something
    const shown = playlists.data?.filter(p => (p.showInLibrary || p.following) && (p.systemKind === 'none' || p.songCount > 0)) ?? [];
    return (
        <>
            {!!recent.data?.length && (
                <>
                    <h2><Link className="h2l" to="/recent">{t('home.continue')}<Icon name="right" /></Link></h2>
                    <RecentList entries={recent.data} />
                </>
            )}
            {playlists.data && shown.length === 0 && (
                // a new person: what to do, in place of an empty shelf
                <p className="muted home-start">
                    {t('home.start')}{' '}<Link to="/playlists/public">{t('home.startLink')}</Link>
                </p>
            )}
            {shown.length > 0 && <h2><Link className="h2l" to="/playlists">{t('home.myPlaylists')}<Icon name="right" /></Link></h2>}
            <div className="shelf">
                {shown.map(p => (
                    <Link key={p.id} className="tile" to={`/playlists/${p.id}`}>
                        <Cover playlist={p} size="tile" />
                        <b>{playlistTitle(p)}</b>
                        <span>{t('playlists.songs', { count: p.songCount })}</span>
                    </Link>
                ))}
            </div>
        </>
    );
}
