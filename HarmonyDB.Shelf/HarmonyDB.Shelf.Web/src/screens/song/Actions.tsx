// What is done with chords and variants, each behind its button: sharing the chords (the link, its QR code; Telegram
// "soon"), adding what one sees to one's own playlist, and the variant's menu "⋯" (my main variant, linking,
// separating, removing, editing my chords).

import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { playlistTitle } from '../../data/playlists';
import { useConfiguration, useSession } from '../../data/session';
import { toast } from '../../app/toasts';
import { addToPlaylist, makeStaticLink, removeStaticLink, removeVariant, useChordsPlaces, useStaticLink, setMainVariant, shareLink, unlinkVariant, useOwnChords, type ChordsVariant, type SongPage } from '../../data/song';
import { offerSignIn } from '../../data/signInOffer';
import { Soon } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { LinkBox } from '../../kit/LinkBox';
import { MenuItem, MenuTitle, Popover } from '../../kit/Popover';
import { startLinking } from './linking';
import { useWide } from '../../app/layout';

/** What a link gives: the song in its playlist (with its variants), or chords alone. */
export type ShareTarget = { kind: 'song'; playlistId: string; songId: string; share: string | null } | { kind: 'chords'; chords: string };

/** The link to the song (from a song) or to the chords (chords alone), with its QR code; Telegram is "soon". */
export function SharePop({ anchor, onClose, target, title, chords }: {
    anchor: HTMLElement | null; onClose: () => void; target: ShareTarget; title: string;
    /** the chords open (from a song: its open variant's), their id and their source's own address */
    chords: { id: string; sourceUri: string | null };
}) {
    const { t } = useTranslation();
    const [link, setLink] = useState<string | null>(null);
    const [failed, setFailed] = useState<unknown>(null);
    useEffect(() => {
        if (!anchor) return;
        let gone = false;
        shareLink(target.kind === 'song'
            ? { kind: 'song', playlistId: target.playlistId, songId: target.songId, share: target.share, carriesAccess: false }
            : { kind: 'chords', chords: target.chords, carriesAccess: false })
            .then(x => !gone && setLink(x))
            .catch(error => !gone && setFailed(error));
        return () => {
            gone = true;
        };
        // the same target: the same link (its parts, not the object made anew at each render)
    }, [anchor, target.kind, target.kind === 'song' ? target.songId : target.chords]);
    return (
        <Popover anchor={anchor} onClose={onClose} width={320} label={t('song.share.title')}>
            <div className="sharepop">
                <b>{title}</b>
                <div className="muted small">{t(target.kind === 'song' ? 'song.share.openSong' : 'song.share.open')}</div>
                <p className="tgrow"><button type="button" className="btn tg" disabled><Icon name="tg" />{t('song.share.telegram')}</button> <Soon /></p>
                {link && <LinkBox link={link} note={t(target.kind === 'song' ? 'song.share.qrSong' : 'song.share.qr')} />}
                {!link && !failed && <p className="loading">{t('common.loading')}</p>}
                {failed !== null && <p className="failed">{errorText(failed)}</p>}
                {anchor && <StaticLink chords={chords} />}
            </div>
        </Popover>
    );
}

/**
 * A link to these chords on the public page, which opens in any browser without the app and shows the chords as the
 * person sees them: the one there is (copy, remove), or the offer to make it. Not for chords of our own sources: their
 * source's address is the public page already. A guest has none.
 */
function StaticLink({ chords }: { chords: { id: string; sourceUri: string | null } }) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const { data: configuration } = useConfiguration();
    const origin = configuration?.publicPageOrigin ?? null;
    const ours = origin !== null && chords.sourceUri !== null && chords.sourceUri.startsWith(origin + '/');
    const offered = signedIn && origin !== null && !ours;
    const mine = useStaticLink(offered ? chords.id : null);
    const make = useMutation({ mutationFn: makeStaticLink });
    const remove = useMutation({ mutationFn: removeStaticLink });
    if (!offered) return null;
    const link = mine.data?.link ?? null;
    const copy = async (address: string) => {
        await navigator.clipboard.writeText(address);
        toast(t('common.copied'));
    };
    return (
        <div className="staticlink">
            <b>{t('song.share.static.title')}</b>
            <div className="muted small">{t('song.share.static.note')}</div>
            {mine.isPending && <p className="loading">{t('common.loading')}</p>}
            {mine.isError && <p className="failed">{errorText(mine.error)}</p>}
            {mine.isSuccess && link?.publicAddress && (
                <>
                    <div className="linkbox"><code>{link.publicAddress}</code><button type="button" className="btn" onClick={() => void copy(link.publicAddress!)}>{t('common.copy')}</button></div>
                    <p><button type="button" className="btn dan sm" disabled={remove.isPending} onClick={() => remove.mutate({ token: link.token, chords: chords.id })}>{t('song.share.static.remove')}</button></p>
                </>
            )}
            {mine.isSuccess && !link && make.data?.refused === 'notOpen' && <p className="muted">{t('song.share.static.notOpen')}</p>}
            {mine.isSuccess && !link && make.data?.refused !== 'notOpen' && (
                <>
                    {make.data?.refused === 'unavailable' && <p className="failed">{t('song.share.static.unavailable')}</p>}
                    <p><button type="button" className="btn sm" disabled={make.isPending} onClick={() => make.mutate(chords.id)}>{t('song.share.static.make')}</button></p>
                </>
            )}
        </div>
    );
}

/** These chords into one of my playlists (those I may add to). */
export function AddPop({ anchor, onClose, chordsId }: { anchor: HTMLElement | null; onClose: () => void; chordsId: string }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { signedIn } = useSession();
    const { data: places } = useChordsPlaces(chordsId);
    // my playlists that hold these chords (ticked, with the song there), and those I may add them to
    const holding = new Map((places?.places ?? []).filter(x => x.chords === chordsId).map(x => [x.playlistId, x.songId]));
    // "My chords" and "My analyses" are filled by the app: not a place to put chords from elsewhere
    const mine = (places?.playlists ?? []).filter(p => p.systemKind !== 'ownChords' && p.systemKind !== 'ownAnalyses' && (p.canAdd || holding.has(p.id)));
    const add = (playlistId: string) => {
        onClose();
        void addToPlaylist({ playlistId, chords: chordsId }).catch(() => undefined);
    };
    return (
        <Popover anchor={anchor} onClose={onClose} width={320} label={t('song.add.title')}>
            <MenuTitle>{t('song.add.title')}</MenuTitle>
            {holding.size > 0 && <p className="note">{t('song.add.inMine', { count: holding.size })}</p>}
            {!signedIn && <MenuItem onClick={() => { onClose(); offerSignIn('change'); }}>{t('nav.signIn')}</MenuItem>}
            {mine.map(p => (
                <div className="it2" key={p.id}>
                    {holding.has(p.id)
                        // already there: the whole row is the way to the song there
                        ? <button type="button" className="it fact" onClick={() => { onClose(); navigate(`/playlists/${p.id}/songs/${holding.get(p.id)}`); }}>
                            <Icon name="check" /><span>{playlistTitle(p)}<small>{t('song.add.here')}</small></span><span className="go">{t('song.add.open')}<Icon name="right" /></span>
                        </button>
                        : <>
                            <MenuItem onClick={() => add(p.id)}><Icon name="plus" />{playlistTitle(p)}</MenuItem>
                            <button type="button" className="goto" aria-label={t('song.add.goTo', { title: playlistTitle(p) })} title={t('song.add.goTo', { title: playlistTitle(p) })}
                                onClick={() => { onClose(); navigate(`/playlists/${p.id}`); }}>
                                <Icon name="right" />
                            </button>
                        </>}
                </div>
            ))}
            {signedIn && places && mine.length === 0 && <p className="note">{t('song.add.none')}</p>}
        </Popover>
    );
}

/** The variant's menu: what is rare is here, not on view. */
export function VariantMenu({ anchor, onClose, page, variant }: { anchor: HTMLElement | null; onClose: () => void; page: SongPage; variant: ChordsVariant }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const wide = useWide();
    const { data: own } = useOwnChords();
    const header = variant.chords.header;
    const playlistId = page.song.playlistId, songId = page.song.id;
    const many = page.variants.length > 1;
    const main = page.myMainItemId === variant.itemId;
    const ownItem = own?.items.find(x => x.chordsId === variant.chords.id) ?? null;
    const named = header && ((header.title ?? '').toLowerCase() !== page.song.title.toLowerCase() || header.artists.join(', ').toLowerCase() !== page.song.artists.join(', ').toLowerCase());
    const run = (action: () => Promise<unknown>) => {
        onClose();
        void action().catch(() => undefined);
    };
    return (
        <Popover anchor={anchor} onClose={onClose} width={320} label={t('song.menu.title')}>
            {header && <MenuTitle>{header.source.title}</MenuTitle>}
            {many && (
                <MenuItem check on={main} sub={t('song.menu.mainSub')}
                    onClick={() => run(() => setMainVariant({ playlistId, songId, itemId: main ? null : variant.itemId }))}>
                    {t('song.menu.main')}
                </MenuItem>
            )}
            {(named || variant.addedBy) && (
                <p className="note">
                    {named && <>{t('song.menu.inSource', { name: `${header!.artists.join(', ')} — ${header!.title ?? ''}` })}<br /></>}
                    {variant.addedBy && t('song.menu.addedBy', { name: variant.addedBy.name })}
                </p>
            )}
            {ownItem && <MenuItem onClick={() => { onClose(); navigate(`/editor?own=${encodeURIComponent(ownItem.id)}`); }}>{t('song.menu.edit')}</MenuItem>}
            {page.canAdd && (
                <>
                    <hr />
                    <MenuItem sub={t('song.menu.linkSub')} onClick={() => {
                        onClose();
                        startLinking(playlistId, songId, page.song.title, page.song.artists);
                        // on a phone the song covers the list: back to the list, where the songs are ticked
                        if (!wide) navigate(`/playlists/${playlistId}`);
                    }}>{t('song.menu.link')}</MenuItem>
                    {many && <MenuItem sub={t('song.menu.unlinkSub')} onClick={() => run(() => unlinkVariant({ playlistId, songId, itemId: variant.itemId }))}>{t('song.menu.unlink')}</MenuItem>}
                    <hr />
                    <MenuItem onClick={() => run(() => removeVariant({ playlistId, songId, itemId: variant.itemId }))}>
                        <span className="danger">{t('song.menu.remove', { title: page.playlist ? playlistTitle(page.playlist) : '' })}</span>
                    </MenuItem>
                </>
            )}
        </Popover>
    );
}
