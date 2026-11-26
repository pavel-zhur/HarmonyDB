// The song beside its list (or over it, on a phone): its illustration behind it, its head, the list of its variants
// (letters, people's marks and keys), its recordings (screens/recordings), the illustrations as
// the item after the variants, and the variant open — its chords (ChordsView). Chords alone (a link, a search result)
// have no list of variants: the pager over the search's results is in their header.

import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/client';
import { errorText } from '../../api/errors';
import { songChordsOnDevice, useOnline } from '../../offline';
import { useQuery } from '@tanstack/react-query';
import { playlistTitle } from '../../data/playlists';
import { useProfile, useSession } from '../../data/session';
import { useSearch } from '../../data/songs';
import { useChordsContent, useChordsPlaces, useIllustrations, useSongPage, type ChordsVariant, type SongPage } from '../../data/song';
import { useDrawnIllustrations, useSongIllustrations } from '../../data/illustrations';
import { useMarkSign } from '../../data/marks';
import { local, useLocalView } from '../../local';
import { Avatar } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { SourceIcon } from '../../kit/SongRow';
import { ToTop } from '../../app/ToTop';
import { ChordsView } from './ChordsView';
import { SongRecordings } from '../recordings/SongRecordings';
import { SongPlayer } from '../../player/SongPlayer';
import { isAnalysis } from '../../data/analysis';
import { VariantMenu } from './Actions';
import { Illustrations } from './Illustrations';
import { SignInOffer } from '../home/GuestHome';
import { Features } from '../home/Features';
import '../home/home.css';
import { SongLinks } from './SongLinks';
import { keyText, letter } from './keys';
import './song.css';
import { personName } from '../../kit/person';

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
    const { signedIn, known } = useSession();
    // a link shared from these chords (?chords=…) opens on them
    const [query] = useSearchParams();
    const linked = query.get('chords');

    // the variant open: chosen here, else the one the link names, else my main one, else the first
    const variant = page && open !== null && open !== 'illustrations'
        ? (open ? page.variants.find(v => v.itemId === open.itemId) : undefined)
            ?? (open === undefined ? page.variants.find(v => v.chords.id === linked) ?? firstOpen(page) : null)
        : null;
    const chordsId = variant?.chords.id ?? null;
    useOpened(chordsId, songId);
    // the song's illustrations, all its variants' (the open one's first): they stay whichever variant is open
    const illustrations = useSongIllustrations(page?.variants.filter(v => v.chords.state === 'open').map(v => v.chords.id) ?? [], chordsId);
    const cover = illustrations.images[0]?.urls['512'] ?? null;

    // the song open here is gone (its last variant removed, here or elsewhere): back to the list, where the toast's
    // Undo brings it back; a link to a song that was never seen here says it is not there
    const navigate = useNavigate();
    const gone = song.isError && !!song.data && song.error instanceof ApiError && song.error.code === 'no_such_song';
    useEffect(() => {
        if (gone) navigate(closeTo, { replace: true });
    }, [gone, closeTo, navigate]);

    if (song.isError) {
        // with no connection, a song whose page is not on the device opens as its chords kept, as a tap in the list does
        return song.error instanceof ApiError && song.error.code === 'not_downloaded'
            ? <SongAsChords playlistId={playlistId} songId={songId} closeTo={closeTo} error={song.error} />
            : <div className="songpage"><div className="hero"><p className="failed">{errorText(song.error)}</p></div></div>;
    }
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
                    <SongRecordings song={{ playlistId, songId, share }} analysed={variants.some(v => isAnalysis(v.chords.header?.kinds))}
                        anyOpen={variants.some(v => v.chords.state === 'open')}
                        analysisId={variant && isAnalysis(variant.chords.header?.kinds) ? variant.chords.id : null} />
                    <div className="vlab">{t('song.chordsLabel')}</div>
                    {cut && w0 > 0 && <button type="button" className="lnk more" onClick={() => setAll(true)}><Icon name="up" />{t('song.moreAbove', { count: w0 })}</button>}
                    {variants.map((v, i) => (cut && (i < w0 || i >= w0 + 5)) ? null : (
                        <VariantRow key={v.itemId} page={page} variant={v} index={i} on={v === variant}
                            onOpen={() => { setOpen(v === variant ? null : { itemId: v.itemId }); setShown(null); }}
                            onKey={semitones => setShown({ chords: v.chords.id, semitones })} />
                    ))}
                    {cut && <button type="button" className="lnk more" onClick={() => setAll(true)}><Icon name="down" />{t('song.showAll', { count: variants.length })}</button>}
                    {(!!illustrations.images.length || illustrations.drawing) && (
                        <button type="button" className={open === 'illustrations' ? 'vrow on' : 'vrow'} onClick={() => setOpen(open === 'illustrations' ? null : 'illustrations')}>
                            <Icon name="img" /><span>{t('song.illustrations', { count: illustrations.images.length })}</span>
                        </button>
                    )}
                    <SongLinks page={page} share={share ?? null} from={(variant ?? firstOpen(page))?.chords.state === 'open' ? (variant ?? firstOpen(page))!.chords.id : null} illustrated={!!illustrations.images.length || illustrations.drawing} onIllustrations={() => setOpen('illustrations')} />
                </div>
            </div>
            {open === 'illustrations' && (
                <Illustrations onClose={() => setOpen(null)}
                    variants={variants.flatMap((v, i) => (v.chords.state === 'open' ? [{ chordsId: v.chords.id, label: variants.length > 1 ? `${letter(i)}. ${v.chords.header?.source.title ?? ''}` : null }] : []))} />
            )}
            {variant && (
                <ChordsView key={variant.chords.id} chordsId={variant.chords.id}
                    place={{ page, variant, share, letter: variants.length > 1 ? letter(index) : null }}
                    shown={shown?.chords === variant.chords.id ? shown.semitones : null}
                    onShow={semitones => setShown(semitones === null ? null : { chords: variant.chords.id, semitones })}
                    onClose={() => setOpen(null)} onSwipe={go} />
            )}
            {!variant && open !== 'illustrations' && !signedIn && known && <div className="guestinv"><Features /><SignInOffer /></div>}
            <SongFoot><SongPlayer song={{ playlistId, songId, share }} analysisId={variant && isAnalysis(variant.chords.header?.kinds) ? variant.chords.id : null} /></SongFoot>
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
            {header?.source.iconUrl && <SourceIcon source={header.source} />}
            <span>{header?.source.title}</span>
            {variant.chords.state === 'notOpen' && <span className="na">{t('song.chords.notOpenShort')}</span>}
            {variant.chords.state === 'unavailable' && <span className="na">{t('song.chords.unavailableShort')}</span>}
            {!!header?.rating && <span className="rt">☆ {header.rating}</span>}
            {page.myMainItemId === variant.itemId && page.variants.length > 1 && <span className="pin">{t('song.main')}</span>}
            {differs && <span className="aka">{t('song.inSource', { name: `${header!.artists.join(', ')} — ${header!.title ?? ''}` })}</span>}
            {people.map(([id, x]) => (
                <span key={id} className="pp">
                    <Avatar person={id === accountId ? { id, name: profile?.displayName ?? t('common.me'), pictureUrl: profile?.pictureUrl ?? null } : person(id)} size="s" />
                    {id === accountId ? t('common.me') : personName(person(id))} {x.marks.map(markSign).join('')}
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
    const online = useOnline();
    const places = useChordsPlaces(chordsId);
    useOpened(chordsId, null);
    // asked only of chords open to me (others answer that they are not)
    const illustrations = useIllustrations(content.data?.state === 'open' ? chordsId : null);
    const cover = illustrations.data?.images[0]?.urls['512'] ?? null;

    // the search the chords were opened from: its chords, in its order
    const q = params.get('q') ?? '';
    const results = useSearch(q, ['songs', 'chords']);
    const ids = results.data?.chords?.map(x => x.id) ?? [];
    const index = ids.indexOf(chordsId);
    const at = (id: string | undefined) => (id ? pathname.slice(0, pathname.lastIndexOf('/') + 1) + encodeURIComponent(id) + search : null);
    const pager = index >= 0 ? { index, count: ids.length, prev: at(ids[index - 1]), next: at(ids[index + 1]) } : undefined;

    const header = content.data?.header;
    // with no connection the app does not know where else the chords are: it says only that they are kept here
    const inMine = new Set(places.data?.places.filter(x => x.chords === chordsId).map(x => x.playlistId) ?? []).size;
    const context = !online
        ? <><Icon name="cloud" />{t('offline.chordsHere')}</>
        : inMine > 0
            ? <><Icon name="pls" />{t('song.chordsInMine', { count: inMine })}</>
            : q
            ? <><Icon name="globe" />{t('song.fromSearch')}</>
            : <><Icon name="link" />{t(signedIn ? 'song.chordsAlone' : 'song.chordsByLink')}</>;

    return (
        <div className="songpage">
            <div className="hero">
                <Hero title={header?.title ?? ''} artists={header?.artists ?? []} context={content.data ? context : null} cover={cover} closeTo={closeTo} />
                {content.isError && <p className="failed">{errorText(content.error)}</p>}
                {!content.data && !content.isError && <p className="loading">{t('common.loading')}</p>}
                {!open && <button type="button" className="vrow" onClick={() => setOpen(true)}>{header?.source.iconUrl && <SourceIcon source={header.source} />}<span>{header?.source.title}</span></button>}
            </div>
            {open && (
                <ChordsView chordsId={chordsId} place={{ pager }} shown={shown} onShow={setShown} onClose={() => setOpen(false)}
                    onSwipe={direction => { const to = direction < 0 ? pager?.prev : pager?.next; if (to) navigate(to); }} />
            )}
            {!open && !signedIn && <div className="guestinv"><Features /><SignInOffer /></div>}
            <SongFoot><SongPlayer song={null} analysisId={isAnalysis(header?.kinds) ? chordsId : null} /></SongFoot>
        </div>
    );
}

/** A song whose page is not on the device, with no connection: its chords kept, alone; else what the reading said. */
function SongAsChords({ playlistId, songId, closeTo, error }: { playlistId: string; songId: string; closeTo: string; error: unknown }) {
    const chords = useQuery({ queryKey: ['song-chords-on-device', playlistId, songId], queryFn: () => songChordsOnDevice(playlistId, songId), networkMode: 'always' });
    if (chords.data) return <ChordsAlone key={chords.data} chordsId={chords.data} closeTo={closeTo} />;
    if (chords.isPending) return null;
    return <div className="songpage"><div className="hero"><p className="failed">{errorText(error)}</p></div></div>;
}
