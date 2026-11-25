// The library: everything from my playlists shown in it, plus what I marked or keep a key or a note on.
// /library, /library/songs/:playlistId/:songId, /library/chords/:chordsId (the song open beside the list).

import { useState } from 'react';
import { Navigate, useLocation, useNavigate, useParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { ListWithSong } from '../../app/panes';
import { useSession } from '../../data/session';
import { playlistTitle, useMyPlaylists } from '../../data/playlists';
import { Chip } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { MenuItem, Popover } from '../../kit/Popover';
import { SongList } from '../lists/SongList';
import { SongPane } from '../song/SongPane';
import { PlaylistMeta } from '../playlists/PlaylistMeta';

export function LibraryScreen() {
    const { known, signedIn } = useSession();
    const { playlistId, songId, chordsId } = useParams();
    const { search } = useLocation();
    if (known && !signedIn) return <Navigate to={'/' + search} replace />;

    const openId = songId ?? chordsId ?? null;
    const song = songId && playlistId ? <SongPane target={{ playlistId, songId }} closeTo={'/library' + search} />
        : chordsId ? <SongPane target={{ chords: chordsId }} closeTo={'/library' + search} /> : null;

    return (
        <ListWithSong songKey={openId} song={song} list={
            <SongList source={{ kind: 'library' }} listKey="library" openId={openId}
                sorts={['popular', 'artist', 'compact', 'title', 'newSongs', 'newChords', 'myMarks']}
                songHref={(p, s) => `/library/songs/${p}/${s}`} chordsHref={id => `/library/chords/${id}`}
                scope={<ScopeChip current={null} />} />
        } />
    );
}

/** The library or one of my playlists: the same list, its scope chosen here. */
export function ScopeChip({ current }: { current: { id: string; title: string } | null }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const { data: playlists } = useMyPlaylists();
    return (
        <>
            <Chip on={current !== null} onClick={e => setAnchor(anchor ? null : e.currentTarget)}>
                <span className="el">{current ? current.title : t('library.whole')}</span><Icon name="down" />
            </Chip>
            <Popover anchor={anchor} onClose={() => setAnchor(null)}>
                <MenuItem on={current === null} sub={t('library.wholeSub')} onClick={() => { setAnchor(null); navigate('/library'); }}>{t('library.whole')}</MenuItem>
                <hr />
                {playlists?.map(p => (
                    <MenuItem key={p.id} on={current?.id === p.id}
                        sub={<><PlaylistMeta playlist={p} />{p.showInLibrary === false ? ` · ${t('library.hiddenSub')}` : ''}</>}
                        onClick={() => { setAnchor(null); navigate(`/playlists/${p.id}`); }}>
                        {playlistTitle(p)}
                    </MenuItem>
                ))}
            </Popover>
        </>
    );
}
