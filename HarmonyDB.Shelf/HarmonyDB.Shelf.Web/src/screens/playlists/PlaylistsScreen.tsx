// My playlists: mine and shared with me, one line each, the "in my library" switch on the right (its label once
// over the column, its meaning small at the bottom); a new one by its title; the public ones and an import by a link.

import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { createPlaylist, followPlaylist, playlistTitle, showInLibrary, useMyPlaylists, type PlaylistSummary } from '../../data/playlists';
import { useSession } from '../../data/session';
import { Avatar, Switch } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { Cover, PlaylistMeta, playlistIcon } from './PlaylistMeta';
import { MyServiceLists } from '../music/MyServiceLists';

export function PlaylistsScreen() {
    const { t } = useTranslation();
    const { accountId } = useSession();
    const playlists = useMyPlaylists();
    const mine = playlists.data?.filter(p => p.owner.id === accountId) ?? [];
    const shared = playlists.data?.filter(p => p.owner.id !== accountId) ?? [];
    return (
        <Page>
            <h1>{t('playlists.title')}</h1>
            <NewPlaylist />
            <p className="quietlinks">
                <Link className="lnk" to="/playlists/public"><Icon name="globe" />{t('playlists.public')}</Link>
                <Link className="lnk" to="/playlists/import"><Icon name="link" />{t('playlists.importOne')}</Link>
            </p>
            {playlists.isError && <p className="failed">{errorText(playlists.error)}</p>}
            {playlists.isPending && <p className="loading">{t('common.loading')}</p>}
            {playlists.data && (
                <>
                    <h2 className="hsw">{t('playlists.mine')}<span title={t('playlists.inLibraryHint')}>{t('playlists.inLibrary')}</span></h2>
                    <div className="pls">{mine.map(p => <PlaylistLine key={p.id} playlist={p} />)}</div>
                    {shared.length > 0 && (
                        <>
                            <h2 className="hsw">{t('playlists.shared')}<span>{t('playlists.inLibrary')}</span></h2>
                            <div className="pls">{shared.map(p => <PlaylistLine key={p.id} playlist={p} />)}</div>
                        </>
                    )}
                    <p className="muted small plnote">{t('playlists.note')}</p>
                </>
            )}
            <MyServiceLists />
        </Page>
    );
}

export function PlaylistLine({ playlist: p }: { playlist: PlaylistSummary }) {
    const { t } = useTranslation();
    const { accountId } = useSession();
    return (
        <div className="plwrap">
            <Link className="pl" to={`/playlists/${p.id}`}>
                <Cover playlist={p} />
                <span style={{ flex: 1, minWidth: 0 }}>
                    <span className="t">{playlistTitle(p)}</span>
                    <span className="m"><Icon name={playlistIcon(p)} /><PlaylistMeta playlist={p} /></span>
                    <span className="m">
                        {p.firstMembers.slice(0, 5).map(m => <Avatar key={m.id} person={m} size="s" />)}
                        {p.owner.id !== accountId && <span>{t('playlists.owner', { name: p.owner.name })}</span>}
                    </span>
                </span>
            </Link>
            {accountId && <InLibrary playlist={p} />}
        </div>
    );
}

/**
 * "In my library": a member shows or hides the playlist's songs there. Anyone else keeps a public playlist (or one
 * opened by its link) by following it — switched on, it follows and its songs come into the library; switched off, it
 * stays among my playlists without its songs in the library (letting it go is on its page). A private one of others:
 * nothing to switch.
 */
export function InLibrary({ playlist: p, share }: { playlist: PlaylistSummary; share?: string | null }) {
    const { t } = useTranslation();
    const show = useMutation({ mutationFn: showInLibrary });
    const follow = useMutation({ mutationFn: followPlaylist });
    if (p.myRole) {
        const on = show.isPending ? show.variables.show : p.showInLibrary ?? false;
        return <Switch on={on} label={t('playlists.inLibraryHint')} onChange={value => show.mutate({ id: p.id, show: value })} />;
    }
    if (p.access === 'private' && !p.following) return null;
    const on = follow.isPending ? true : show.isPending ? show.variables.show : p.following && p.showInLibrary !== false;
    return <Switch on={on} label={t('playlists.inLibraryHint')}
        onChange={value => (p.following ? show.mutate({ id: p.id, show: value }) : value ? follow.mutate({ id: p.id, share }) : undefined)} />;
}

function NewPlaylist() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [title, setTitle] = useState('');
    const create = useMutation({ mutationFn: createPlaylist, onSuccess: r => navigate(`/playlists/${r.value.playlist.id}`) });
    return (
        <form className="inline-form" onSubmit={e => { e.preventDefault(); if (title.trim()) create.mutate(title.trim()); }}>
            <input className="field" value={title} onChange={e => setTitle(e.target.value)} placeholder={t('playlists.newPlaceholder')} aria-label={t('playlists.newPlaceholder')} />
            <button type="submit" className="btn" disabled={!title.trim() || create.isPending}><Icon name="plus" />{t('common.create')}</button>
        </form>
    );
}
