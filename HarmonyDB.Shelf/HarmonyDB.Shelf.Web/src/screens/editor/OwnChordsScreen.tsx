// The chords I wrote (/own-chords): each by its title, open and edit; one that is in none of my playlists (saved, but
// not put in one just then) says so, and is put in a playlist from here — as a new song of it.

import { useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api, call, type Schemas } from '../../api/client';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatDate } from '../../i18n';
import { placeOwnChords } from '../../data/ownChords';
import { playlistTitle } from '../../data/playlists';
import { useOwnChords } from '../../data/song';
import { useSession } from '../../data/session';
import { Icon } from '../../kit/Icon';
import { MenuItem, MenuTitle, Popover } from '../../kit/Popover';
import './editor.css';

type Item = Schemas['OwnChordsItem'];

export function OwnChordsScreen() {
    const { t } = useTranslation();
    const own = useOwnChords();
    const items = own.data?.items ?? [];
    const ids = items.map(x => x.chordsId).filter((x): x is string => !!x);
    const places = usePlacesOf(ids);
    const placed = new Set(places.data?.places.map(x => x.chords) ?? []);
    // my playlists I may add to; of the system ones only "My chords" (not imported chords, not analyses)
    const where = (places.data?.playlists ?? []).filter(p => p.canAdd && (p.systemKind === 'none' || p.systemKind === 'ownChords'))
        .sort((a, b) => (a.systemKind === 'ownChords' ? -1 : b.systemKind === 'ownChords' ? 1 : 0));
    return (
        <Page>
            <h1>{t('editor.own.title')}</h1>
            <p className="quietlinks"><Link className="lnk" to="/editor"><Icon name="plus" />{t('editor.own.new')}</Link></p>
            {own.isError && <p className="failed">{errorText(own.error)}</p>}
            {own.isPending && <p className="loading">{t('common.loading')}</p>}
            {own.data && !items.length && <p className="muted">{t('editor.own.empty')}</p>}
            <div className="lines">
                {items.map(item => (
                    <Line key={item.id} item={item} inNone={places.data !== undefined && (!item.chordsId || !placed.has(item.chordsId))} where={where} />
                ))}
            </div>
        </Page>
    );
}

function usePlacesOf(ids: string[]) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['places', 'own', ids.join()],
        queryFn: () => call(api.GET('/api/library/places', { params: { query: { chords: ids } } })),
        enabled: signedIn,
    });
}

function Line({ item, inNone, where }: { item: Item; inNone: boolean; where: Schemas['MyPlaylist'][] }) {
    const { t } = useTranslation();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const place = useMutation({ mutationFn: placeOwnChords });
    const title = item.text.title, artists = item.text.artists;
    return (
        <div className="ed-own">
            <span className="ed-own-t">
                {item.chordsId ? <Link to={`/chords/${encodeURIComponent(item.chordsId)}`}><b>{title}</b></Link> : <b>{title}</b>}
                <span className="muted small">{[artists.join(', '), formatDate(item.updatedAt)].filter(Boolean).join(' · ')}</span>
                {inNone && (item.chordsId
                    ? <span className="small ed-own-none">{t('editor.own.inNone')}</span>
                    : <span className="small ed-own-none">{t('editor.own.notReachable')}</span>)}
            </span>
            {inNone && item.chordsId && (
                <>
                    <button type="button" className="btn sm" disabled={place.isPending} onClick={e => setAnchor(anchor ? null : e.currentTarget)}>
                        <Icon name="addpl" />{t('editor.own.putIn')}
                    </button>
                    <Popover anchor={anchor} onClose={() => setAnchor(null)} label={t('editor.own.putIn')}>
                        <MenuTitle>{t('editor.own.putInTitle')}</MenuTitle>
                        {where.map(p => (
                            <MenuItem key={p.id} onClick={() => {
                                setAnchor(null);
                                place.mutate({ playlistId: p.id, chords: item.chordsId!, title, artists });
                            }}>{playlistTitle(p)}</MenuItem>
                        ))}
                    </Popover>
                </>
            )}
            {item.editable && <Link className="btn sm" to={`/editor?own=${encodeURIComponent(item.id)}`}><Icon name="pen" />{t('editor.own.edit')}</Link>}
        </div>
    );
}
