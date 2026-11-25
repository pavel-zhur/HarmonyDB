// Linking songs of a playlist: "Link with another song" in a variant's menu starts it; while it goes, the rows of that
// playlist's list carry a chain sign to tick the songs to join (<LinkPick>), and a bar at the bottom (<LinkBar>, put
// once by the list) says how many are ticked, asks the name of the song they make and links them (one call).
// Nothing of it is seen when no linking goes on.

import { useState, useSyncExternalStore } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { linkSongs } from '../../data/song';
import { Icon } from '../../kit/Icon';

interface Linking {
    playlistId: string;
    songId: string;
    title: string;
    artists: string[];
    picked: readonly string[];
}

let linking: Linking | null = null;
const listeners = new Set<() => void>();
const set = (value: Linking | null) => {
    linking = value;
    listeners.forEach(listener => listener());
};

function useLinking(): Linking | null {
    return useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => linking);
}

export function startLinking(playlistId: string, songId: string, title: string, artists: string[]): void {
    set({ playlistId, songId, title, artists, picked: [] });
}

/** The chain sign of a row while linking in its playlist: a tap ticks the song (the row is not opened). */
export function LinkPick({ playlistId, songId }: { playlistId: string; songId: string | null | undefined }) {
    const { t } = useTranslation();
    const now = useLinking();
    if (!now || now.playlistId !== playlistId || !songId || songId === now.songId) return null;
    const on = now.picked.includes(songId);
    const toggle = (e: React.SyntheticEvent) => {
        e.preventDefault();
        e.stopPropagation();
        set({ ...now, picked: on ? now.picked.filter(x => x !== songId) : [...now.picked, songId] });
    };
    return (
        <span className={on ? 'linkpick on' : 'linkpick'} role="checkbox" aria-checked={on} tabIndex={0} aria-label={t('song.link.pick')} title={t('song.link.pick')}
            onClick={toggle} onKeyDown={e => (e.key === 'Enter' || e.key === ' ') && toggle(e)}>
            <Icon name={on ? 'check' : 'link'} />
        </span>
    );
}

/** The bar of linking, at the bottom of the playlist's list. */
export function LinkBar({ playlistId }: { playlistId: string }) {
    const { t } = useTranslation();
    const now = useLinking();
    const [naming, setNaming] = useState(false);
    const [title, setTitle] = useState('');
    const [artists, setArtists] = useState('');
    const [busy, setBusy] = useState(false);
    if (!now || now.playlistId !== playlistId) return null;

    const name = () => {
        setTitle(now.title);
        setArtists(now.artists.join(', '));
        setNaming(true);
    };
    const link = async () => {
        setBusy(true);
        try {
            await linkSongs({
                playlistId: now.playlistId, songId: now.songId, songIds: [...now.picked],
                title: title.trim() || now.title, artists: artists.split(',').map(x => x.trim()).filter(Boolean),
            });
            setNaming(false);
            set(null);
        } catch {
            // said in a toast
        } finally {
            setBusy(false);
        }
    };
    const cancel = () => {
        setNaming(false);
        set(null);
    };

    return createPortal(
        <div className="linkbar" role="region" aria-label={t('song.link.title')}>
            {!naming ? (
                <>
                    <span className="linkhint">{now.picked.length ? t('song.link.picked', { count: now.picked.length, title: now.title }) : t('song.link.hint', { title: now.title })}</span>
                    <button type="button" className="btn" onClick={cancel}>{t('common.cancel')}</button>
                    <button type="button" className="btn pri" disabled={!now.picked.length} onClick={name}><Icon name="link" />{t('song.link.do')}</button>
                </>
            ) : (
                <form className="linkname" onSubmit={e => { e.preventDefault(); void link(); }}>
                    <span className="small muted">{t('song.link.name')}</span>
                    <input className="field" value={artists} onChange={e => setArtists(e.target.value)} placeholder={t('song.link.artists')} aria-label={t('song.link.artists')} />
                    <input className="field" value={title} onChange={e => setTitle(e.target.value)} placeholder={t('song.link.songTitle')} aria-label={t('song.link.songTitle')} autoFocus />
                    <span className="chips">
                        <button type="button" className="btn" onClick={() => setNaming(false)}>{t('common.cancel')}</button>
                        <button type="submit" className="btn pri" disabled={busy}><Icon name="link" />{t('song.link.do')}</button>
                    </span>
                </form>
            )}
        </div>, document.getElementById('root')!);
}
