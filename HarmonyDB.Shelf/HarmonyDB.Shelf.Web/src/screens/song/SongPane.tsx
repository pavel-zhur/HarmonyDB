// The song beside its list (or over it, on a phone): its illustration behind it, its head, the list of its variants
// (letters, people's marks and keys), its recordings (screens/recordings), the illustrations as
// the item after the variants, and the variant open — its chords (ChordsView). Chords alone (a link, a search result)
// have no list of variants: the pager over the search's results is in their header.

import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { playlistTitle } from '../../data/playlists';
import { useProfile, useSession } from '../../data/session';
import { useSearch } from '../../data/songs';
import { useChordsContent, useIllustrations, useSongPage, type ChordsVariant, type SongPage } from '../../data/song';
import { useMarkSign } from '../../data/marks';
import { local, useLocalView } from '../../local';
import { Avatar } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { SourceIcon } from '../../kit/SongRow';
import { ToTop } from '../../app/ToTop';
import { ChordsView } from './ChordsView';
import { SongRecordings } from '../recordings/SongRecordings';
import { SongPlayer } from '../../player/SongPlayer';
import { VariantMenu } from './Actions';
import { Illustrations } from './Illustrations';
import { keyText, letter } from './keys';
import './song.css';

export type SongTarget = { playlistId: string; songId: string; share?: string | null } | { chords: string };

export function SongPane({ target, closeTo }: { target: SongTarget; closeTo: string }) {
    return 'songId' in target
        ? <SongInPlaylist key={target.songId} playlistId={target.playlistId} songId={target.songId} share={target.share ?? null} closeTo={closeTo} />
        : <ChordsAlone key={target.chords} chordsId={target.chords} closeTo={closeTo} />;
}

/** "Opened", for the recent: once per opening of a song's variant or of chords. */
function useOpened(chords: string | null, songId: string | null) {
    const { signedIn } = useSession();
    useEffect(() => {
        if (signedIn && chords) local.opened({ chords, songId });
    }, [chords, songId, signedIn]);
}

function Hero({ title, artists, context, cover, closeTo }: { title: string; artists: readonly string[]; context: React.ReactNode; cover: string | null; closeTo: string }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    return (
        <>
            {cover && <div className="bgart" style={{ backgroundImage: `url(${cover})` }} />}
            <div className="htop">
                <div className="t">
                    <div className="a">{artists.join(', ')}</div>
                    <div className="n">{title}</div>
                    {context && <div className="ctx">{context}</div>}
                </div>
                <button type="button" className="xbtn" aria-label={t('nav.close')} title={t('nav.close')} onClick={() => navigate(closeTo)}><Icon name="x" /></button>
            </div>
        </>
    );
}

/**
 * The bottom of the song page, held at the pane's bottom: the player's bar (when there is one) and "to top" standing
 * just above whatever is there — placed by the bar, so the two never sit on each other in any layout.
 */
function SongFoot({ children }: { children: React.ReactNode }) {
    return (
        <div className="songfoot">
            <ToTop />
            {children}
        </div>
    );
}

const firstOpen = (page: SongPage) => page.variants.find(v => v.itemId === page.myMainItemId) ?? page.variants.find(v => v.chords.state === 'open') ?? page.variants[0] ?? null;

function SongInPlaylist({ playlistId, songId, share, closeTo }: { playlistId: string; songId: string; share: string | null; closeTo: string }) {
    const { t } = useTranslation();
    const song = useSongPage(playlistId, songId, share);
    const page = song.data;
    const [open, setOpen] = useState<{ itemId: string } | 'illustrations' | null | undefined>(undefined);
    const [shown, setShown] = useState<{ chords: string; semitones: number } | null>(null);
    const [all, setAll] = useState(false);

    // the variant open: chosen here, else my main one, else the first
    const variant = page && open !== null && open !== 'illustrations'
        ? (open ? page.variants.find(v => v.itemId === open.itemId) : undefined) ?? (open === undefined ? firstOpen(page) : null)
        : null;
    const chordsId = variant?.chords.id ?? null;
    useOpened(chordsId, songId);
    const illustrations = useIllustrations(chordsId ?? (page ? firstOpen(page)?.chords.id ?? null : null));
    const cover = illustrations.data?.images[0]?.urls['512'] ?? null;

    if (song.isError) return <div className="songpage"><div className="hero"><p className="failed">{errorText(song.error)}</p></div></div>;
    if (!page) return <div className="songpage"><div className="hero"><p className="loading">{t('common.loading')}</p></div></div>;

    const variants = page.variants;
    const index = variant ? variants.indexOf(variant) : -1;
    const cut = variants.length > 6 && !all;
    const w0 = cut ? Math.max(0, Math.min(Math.max(index, 0) - 2, variants.length - 5)) : 0;
    const go = (direction: -1 | 1) => {
        const next = variants[index + direction];
        if (next) {
            setOpen({ itemId: next.itemId });
            setShown(null);
        }
    };

    const context = page.fromPrivatePlaylist
        ? <><Icon name="lock" />{t('song.fromPrivate')}</>
        : page.playlist ? <Link className="ctxl" to={`/playlists/${page.playlist.id}${share ? `?share=${encodeURIComponent(share)}` : ''}`}><Icon name="pls" />{playlistTitle(page.playlist)}<Icon name="right" /></Link> : null;

    return (
        <div className="songpage">
            <div className="hero">
                <Hero title={page.song.title} artists={page.song.artists} context={context} cover={cover} closeTo={closeTo} />
                <div className="vers">
                    <SongRecordings song={{ playlistId, songId, share }} analysed={variants.some(v => !!v.chords.header?.kinds.includes('timeline'))} />
                    <div className="vlab">{t('song.chordsLabel')}</div>
                    {cut && w0 > 0 && <button type="button" className="lnk more" onClick={() => setAll(true)}><Icon name="up" />{t('song.moreAbove', { count: w0 })}</button>}
                    {variants.map((v, i) => (cut && (i < w0 || i >= w0 + 5)) ? null : (
                        <VariantRow key={v.itemId} page={page} variant={v} index={i} on={v === variant}
                            onOpen={() => { setOpen(v === variant ? null : { itemId: v.itemId }); setShown(null); }}
                            onKey={semitones => setShown({ chords: v.chords.id, semitones })} />
                    ))}
                    {cut && <button type="button" className="lnk more" onClick={() => setAll(true)}><Icon name="down" />{t('song.showAll', { count: variants.length })}</button>}
                    {!!illustrations.data?.images.length && (
                        <button type="button" className={open === 'illustrations' ? 'vrow on' : 'vrow'} onClick={() => setOpen(open === 'illustrations' ? null : 'illustrations')}>
                            <Icon name="img" /><span>{t('song.illustrations', { count: illustrations.data.images.length })}</span>
                        </button>
                    )}
                </div>
            </div>
            {open === 'illustrations' && illustrations.data && <Illustrations images={illustrations.data.images} onClose={() => setOpen(null)} />}
            {variant && (
                <ChordsView key={variant.chords.id} chordsId={variant.chords.id}
                    place={{ page, variant, share, letter: variants.length > 1 ? letter(index) : null }}
                    shown={shown?.chords === variant.chords.id ? shown.semitones : null}
                    onShow={semitones => setShown(semitones === null ? null : { chords: variant.chords.id, semitones })}
                    onClose={() => setOpen(null)} onSwipe={go} />
            )}
            <SongFoot><SongPlayer song={{ playlistId, songId, share }} analysisId={variant?.chords.header?.kinds.includes('timeline') ? variant.chords.id : null} /></SongFoot>
        </div>
    );
}

/** A variant: its letter, its source, my main one, its name in the source when it differs, the people's marks and keys. */
function VariantRow({ page, variant, index, on, onOpen, onKey }: {
    page: SongPage; variant: ChordsVariant; index: number; on: boolean; onOpen: () => void; onKey: (semitones: number) => void;
}) {
    const { t } = useTranslation();
    const { accountId, signedIn } = useSession();
    const { data: profile } = useProfile();
    const markSign = useMarkSign();
    const view = useLocalView();
    const [menu, setMenu] = useState<HTMLElement | null>(null);
    const header = variant.chords.header;
    const chords = variant.chords.id;
    const differs = header && ((header.title ?? '').toLowerCase() !== page.song.title.toLowerCase() || header.artists.join(', ').toLowerCase() !== page.song.artists.join(', ').toLowerCase());

    // the people with marks or keys on these chords: me first (as I see them now), then the others
    const people = useMemo(() => {
        const result = new Map<string, { marks: string[]; mine: number | null; here: number | null }>();
        const of = (id: string) => {
            if (!result.has(id)) result.set(id, { marks: [], mine: null, here: null });
            return result.get(id)!;
        };
        if (accountId) {
            const me = of(accountId);
            me.marks = Object.values(view.marks).filter(m => m.chords === chords).map(m => m.kind);
            me.mine = Object.values(view.keys).find(k => k.chords === chords && !k.playlistId)?.semitones ?? null;
            const here = Object.values(view.keys).find(k => k.chords === chords && k.playlistId === page.song.playlistId);
            me.here = here?.semitones ?? null;
        }
        for (const m of page.marks) if (m.chords === chords && m.accountId !== accountId) of(m.accountId).marks.push(m.kind);
        for (const k of page.keys) {
            if (k.chords !== chords || k.accountId === accountId) continue;
            if (k.here) of(k.accountId).here = k.semitones;
            else of(k.accountId).mine = k.semitones;
        }
        return [...result].filter(([, x]) => x.marks.length || x.mine !== null || x.here !== null);
    }, [page, chords, accountId, view]);

    const person = (id: string) => page.people.find(x => x.id === id) ?? { id, name: '?', pictureUrl: null, gone: false };
    const keyButton = (semitones: number, here: boolean) => on
        ? <span className={here ? 'k h tap' : 'k tap'} role="button" tabIndex={0} title={t('song.key.apply')}
            onClick={e => { e.stopPropagation(); onKey(semitones); }} onKeyDown={e => { if (e.key === 'Enter') { e.stopPropagation(); onKey(semitones); } }}>
            {here ? t('song.key.here', { key: keyText(semitones) }) : keyText(semitones)}
        </span>
        : <span className={here ? 'k h' : 'k'}>{here ? t('song.key.here', { key: keyText(semitones) }) : keyText(semitones)}</span>;

    return (
        <div className={on ? 'vrow on' : 'vrow'} role="button" tabIndex={0} aria-pressed={on} onClick={onOpen} onKeyDown={e => e.key === 'Enter' && e.target === e.currentTarget && onOpen()}>
            <span className="l">{letter(index)}.</span>
            {header && <SourceIcon source={header.source} />}
            <span>{header?.source.title}</span>
            {variant.chords.state === 'notOpen' && <span className="na">{t('song.chords.notOpenShort')}</span>}
            {variant.chords.state === 'unavailable' && <span className="na">{t('song.chords.unavailableShort')}</span>}
            {!!header?.rating && <span className="rt">☆ {header.rating}</span>}
            {page.myMainItemId === variant.itemId && page.variants.length > 1 && <span className="pin">{t('song.main')}</span>}
            {differs && <span className="aka">{t('song.inSource', { name: `${header!.artists.join(', ')} — ${header!.title ?? ''}` })}</span>}
            {people.map(([id, x]) => (
                <span key={id} className="pp">
                    <Avatar person={id === accountId ? { id, name: profile?.displayName ?? t('common.me'), pictureUrl: profile?.pictureUrl ?? null } : person(id)} size="s" />
                    {id === accountId ? t('common.me') : person(id).name} {x.marks.map(markSign).join('')}
                    {x.mine !== null && <> {keyButton(x.mine, false)}</>}
                    {x.here !== null && <> {keyButton(x.here, true)}</>}
                </span>
            ))}
            {signedIn && (
                <span className="vm" role="button" tabIndex={0} aria-label={t('song.menu.of', { letter: letter(index) })} title={t('song.menu.title')}
                    onClick={e => { e.stopPropagation(); setMenu(menu ? null : e.currentTarget); }}
                    onKeyDown={e => { if (e.key === 'Enter') { e.stopPropagation(); setMenu(e.currentTarget); } }}>
                    <Icon name="dots" />
                </span>
            )}
            {menu && <span onClick={e => e.stopPropagation()}><VariantMenu anchor={menu} onClose={() => setMenu(null)} page={page} variant={variant} /></span>}
        </div>
    );
}

/** Chords alone: by a link, or a result of a search (then the pager over the results, kept in the address's query). */
function ChordsAlone({ chordsId, closeTo }: { chordsId: string; closeTo: string }) {
    const { t } = useTranslation();
    const content = useChordsContent(chordsId);
    const { pathname, search } = useLocation();
    const [params] = useSearchParams();
    const navigate = useNavigate();
    const [shown, setShown] = useState<number | null>(null);
    const [open, setOpen] = useState(true);
    const { signedIn } = useSession();
    useOpened(chordsId, null);
    const illustrations = useIllustrations(chordsId);
    const cover = illustrations.data?.images[0]?.urls['512'] ?? null;

    // the search the chords were opened from: its chords, in its order
    const q = params.get('q') ?? '';
    const results = useSearch(q, ['songs', 'chords']);
    const ids = results.data?.chords?.map(x => x.id) ?? [];
    const index = ids.indexOf(chordsId);
    const at = (id: string | undefined) => (id ? pathname.slice(0, pathname.lastIndexOf('/') + 1) + encodeURIComponent(id) + search : null);
    const pager = index >= 0 ? { index, count: ids.length, prev: at(ids[index - 1]), next: at(ids[index + 1]) } : undefined;

    const header = content.data?.header;
    const context = q
        ? <><Icon name="globe" />{t('song.fromSearch')}</>
        : <><Icon name="link" />{t(signedIn ? 'song.chordsAlone' : 'song.chordsByLink')}</>;

    return (
        <div className="songpage">
            <div className="hero">
                <Hero title={header?.title ?? ''} artists={header?.artists ?? []} context={content.data ? context : null} cover={cover} closeTo={closeTo} />
                {content.isError && <p className="failed">{errorText(content.error)}</p>}
                {!content.data && !content.isError && <p className="loading">{t('common.loading')}</p>}
                {!open && <button type="button" className="vrow" onClick={() => setOpen(true)}>{header && <SourceIcon source={header.source} />}<span>{header?.source.title}</span></button>}
            </div>
            {open && (
                <ChordsView chordsId={chordsId} place={{ pager }} shown={shown} onShow={setShown} onClose={() => setOpen(false)}
                    onSwipe={direction => { const to = direction < 0 ? pager?.prev : pager?.next; if (to) navigate(to); }} />
            )}
            <SongFoot><SongPlayer song={null} analysisId={header?.kinds.includes('timeline') ? chordsId : null} /></SongFoot>
        </div>
    );
}
