// A playlist: its head (cover, title, what it is, the book, its settings, the members; the update button for the one
// who imported it) over its list — the library's list, scoped to it. /playlists/:id, /playlists/:id/songs/:songId.
// Opened by its link: ?share=<token>.

import { Link, useLocation, useParams, useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/client';
import { errorText } from '../../api/errors';
import { ListWithSong } from '../../app/panes';
import { playlistTitle, unfollowPlaylist, usePlaylist, type PlaylistDetails } from '../../data/playlists';
import { useMutation } from '@tanstack/react-query';
import { useSession } from '../../data/session';
import { offerSignIn } from '../../data/signInOffer';
import { Avatar } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { SongList } from '../lists/SongList';
import { SongPane } from '../song/SongPane';
import { ScopeChip } from '../library/LibraryScreen';
import { Cover, PlaylistMeta } from './PlaylistMeta';
import { OriginSync } from './OriginSync';
import { InLibrary } from './PlaylistsScreen';
import './playlists.css';

export function PlaylistScreen() {
    const { t } = useTranslation();
    const { id, songId } = useParams() as { id: string; songId?: string };
    const [params] = useSearchParams();
    const { search } = useLocation();
    const share = params.get('share');
    const playlist = usePlaylist(id, share);
    const { signedIn } = useSession();

    if (playlist.isError) {
        const closed = playlist.error instanceof ApiError && playlist.error.code === 'no_such_playlist' && !signedIn;
        return (
            <div className="pane"><div className="pad">
                {closed ? <Locked /> : <p className="failed">{errorText(playlist.error)}</p>}
            </div></div>
        );
    }

    const song = songId ? <SongPane target={{ playlistId: id, songId, share }} closeTo={`/playlists/${id}${search}`} /> : null;
    const details = playlist.data;
    return (
        <ListWithSong songKey={songId ?? null} song={song} list={
            <SongList source={{ kind: 'playlist', id, share }} listKey={`playlist:${id}`} openId={songId ?? null}
                sorts={['order', 'popular', 'artist', 'compact', 'title', 'newSongs', 'newChords', 'myMarks']}
                songHref={(p, s) => (p === id ? `/playlists/${id}/songs/${s}` : `/playlists/${p}/songs/${s}`)}
                chordsHref={c => `/library/chords/${c}`}
                head={details ? <Head details={details} share={share} /> : <p className="loading">{t('common.loading')}</p>}
                scope={signedIn && details ? <ScopeChip current={{ id, title: playlistTitle(details.playlist) }} /> : undefined}
                movable={!!details?.canAdminister} />
        } />
    );
}

function Head({ details, share }: { details: PlaylistDetails; share: string | null }) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const p = details.playlist;
    const member = !!p.myRole;
    const settings = `/playlists/${p.id}/settings`;
    return (
        <>
            <div className="scopehead">
                <Cover playlist={p} size="big" />
                <div style={{ flex: 1, minWidth: 0 }}>
                    <div className="t" title={playlistTitle(p)}>{playlistTitle(p)}</div>
                    <div className="muted small"><PlaylistMeta playlist={p} /></div>
                </div>
                {/* the book, as the mock-up offers it to everyone: a guest signs in there; a profile without PDF books is
                    told there what it takes */}
                <Link className="xbtn" to={`/book?playlist=${p.id}${share ? `&share=${share}` : ''}`} aria-label={t('playlist.book')} title={t('playlist.book')}><Icon name="file" /></Link>
                {signedIn && member && <Link className="xbtn" to={settings} aria-label={t('playlist.settings')} title={t('playlist.settings')}><Icon name="share" /></Link>}
            </div>
            {member && (
                <Link className="ppl" to={settings} aria-label={t('playlist.members')}>
                    {details.members.slice(0, 6).map(m => <Avatar key={m.person.id} person={m.person} size="s" />)}
                    <span>{t('playlist.members')}</span>
                </Link>
            )}
            {!member && (p.access !== 'private' || p.following) && (
                <div className="keep">
                    <span>{t('playlists.inLibraryHint')}</span>
                    <InLibrary playlist={p} share={share} />
                    {p.following && <LetGo id={p.id} />}
                </div>
            )}
            {p.origin?.canSync && <OriginSync details={details} />}
        </>
    );
}

/** A playlist followed, let go: it leaves my playlists (Undo in the toast). */
function LetGo({ id }: { id: string }) {
    const { t } = useTranslation();
    const unfollow = useMutation({ mutationFn: unfollowPlaylist });
    return <button type="button" className="lnk" disabled={unfollow.isPending} onClick={() => unfollow.mutate(id)}>{t('playlists.letGo')}</button>;
}

/** A private playlist opened by a guest: a lock, and signing in. */
function Locked() {
    const { t } = useTranslation();
    return (
        <div className="locked">
            <span className="lockic"><Icon name="lock" /></span>
            <h1>{t('playlist.locked')}</h1>
            <p className="muted">{t('playlist.lockedText')}</p>
            <button type="button" className="btn pri" onClick={() => offerSignIn('open')}>{t('nav.signIn')}</button>
        </div>
    );
}
