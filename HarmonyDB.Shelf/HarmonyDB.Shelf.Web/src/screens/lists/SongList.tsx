// A list of songs with its search and filters — the library's and a playlist's (and the home's search, as the
// library's): live search as one types (the cursor stays), the sort, whose marks / which mark; read in pages as it is
// scrolled; while searching, below the list: the songs of my other playlists and chords from the sources.

import { Fragment, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useSession } from '../../data/session';
import { rowId, useSearch, useSongList, type SongRow as Row, type SongSource } from '../../data/songs';
import { useMarkKinds, useMarkSign } from '../../data/marks';
import { errorText } from '../../api/errors';
import { Chip, SearchField } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { MenuItem, MenuTitle, Popover } from '../../kit/Popover';
import { SongRow, Highlight } from '../../kit/SongRow';
import { scrollerOf } from '../../app/layout';
import { setLastOpened, useLastOpened } from '../../app/lastOpened';
import { myMarksOn, useLocalView } from '../../local';
import { serverParams, useListState, type ListSort } from './listParams';
import { LinkBar, LinkPick } from '../song/linking';
import { MusicSearch } from '../music/MusicSearch';
import { notOnDevice } from '../../offline';

export interface SongListProps {
    source: SongSource;
    /** the list's name for "the one I came back from" */
    listKey: string;
    /** the sorts offered, the first the default */
    sorts: ListSort[];
    /** the address of a row (the list's own query is added) */
    songHref: (playlistId: string, songId: string) => string;
    chordsHref: (chordsId: string) => string;
    /** the row open beside the list now (a song's or chords' id) */
    openId: string | null;
    /** before the search: the playlist's head; the scope chip */
    head?: ReactNode;
    scope?: ReactNode;
    /** the search field alone, with the results under it (the home): no filters */
    searchOnly?: { placeholder: string };
    /** after the list, in the same column (the home under its search) */
    children?: ReactNode;
}

const sortLabels: Record<ListSort, [string, string]> = {
    popular: ['library.sort.popular', 'library.sort.popularFull'],
    artist: ['library.sort.artist', 'library.sort.artist'],
    compact: ['library.sort.compact', 'library.sort.compactFull'],
    title: ['library.sort.title', 'library.sort.title'],
    newSongs: ['library.sort.newSongs', 'library.sort.newSongs'],
    newChords: ['library.sort.newChords', 'library.sort.newChords'],
    myMarks: ['library.sort.myMarks', 'library.sort.myMarksFull'],
    order: ['library.sort.order', 'library.sort.orderFull'],
};

export function SongList(p: SongListProps) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const [state, update, query] = useListState();
    const sort = state.sort && p.sorts.includes(state.sort) ? state.sort : p.sorts[0]!;
    const params = serverParams({ ...state, sort });
    const searching = state.q.trim().length > 0;
    const list = useSongList(p.source, params, (!p.searchOnly || searching) && (p.source.kind !== 'library' || signedIn));
    const rows = useMemo(() => list.data?.pages.flatMap(page => page.rows) ?? [], [list.data]);
    const total = list.data?.pages[0]?.total ?? 0;
    const people = list.data?.pages[0]?.people ?? [];
    const last = useLastOpened(p.listKey);
    // my marks as I see them now (a mark made a moment ago, not sent yet, too)
    const local = useLocalView();
    const coverSlot = rows.some(row => row.cover);
    const lit = p.openId ?? last;

    // the count in the placeholder is the whole list's, kept while searching
    const wholeCount = useRef(0);
    if (!searching && !state.mark && list.data) wholeCount.current = total;

    // the field types freely; the address (and the list) follow a moment later
    const [text, setText] = useState(state.q);
    const pushed = useRef(state.q);
    const timer = useRef<ReturnType<typeof setTimeout>>();
    useEffect(() => {
        if (state.q !== pushed.current) {
            pushed.current = state.q;
            setText(state.q);
        }
    }, [state.q]);
    const type = (value: string) => {
        setText(value);
        clearTimeout(timer.current);
        timer.current = setTimeout(() => {
            pushed.current = value;
            update({ q: value });
        }, 200);
    };

    const href = (path: string) => (query ? `${path}?${query}` : path);
    const open = (row: Row) => setLastOpened(p.listKey, rowId(row));

    return (
        <div className="pad">
            {p.head}
            <SearchField value={text} onChange={type}
                placeholder={p.searchOnly?.placeholder ?? t('library.searchPlaceholder', { count: wholeCount.current })} />
            {!p.searchOnly && (
                <div className="filters">
                    {p.scope}
                    <SortChip sort={sort} sorts={p.sorts} onSort={s => update({ sort: s === p.sorts[0] ? null : s })} />
                    {signedIn && <WhoChip who={state.who} mark={state.mark} people={people} onChange={update} />}
                </div>
            )}

            {(!p.searchOnly || searching) && (
                <div className="rows">
                    {list.isError && <p className="failed">{errorText(list.error)}</p>}
                    {list.isPending && list.fetchStatus !== 'idle' && <p className="loading">{t('common.loading')}</p>}
                    {list.isSuccess && rows.length === 0 && !searching && <p className="empty">{t('library.empty')}</p>}
                    {p.searchOnly && list.isSuccess && rows.length > 0 && <p className="muted small" style={{ padding: '10px 8px 0', margin: 0 }}>{t('home.inMyPlaylists', { count: total })}</p>}
                    {sort === 'compact'
                        ? <Compact rows={rows} q={state.q} lit={lit} href={row => rowHref(row, p, href)} onOpen={open} />
                        : rows.map(row => {
                            const id = rowId(row);
                            return (
                                <SongRow key={id} to={rowHref(row, p, href)} lit={lit === id} q={state.q} title={row.title} artists={row.artists}
                                    myMarks={myMarksOn(local, row.chordsIds) ?? row.myMarks} cover={row.cover} coverSlot={coverSlot} otherMarks={row.otherMarks} otherPeople={row.otherPeople}
                                    sources={row.sources} moreSources={Math.max(0, row.chordsCount - row.sources.length)}
                                    extra={notOnDevice(row) ? <span className="na muted">{t('offline.notKept')}</span>
                                        : row.chords && row.chords.state !== 'open' ? <span className="na muted">{t(row.chords.state === 'notOpen' ? 'library.notOpen' : 'library.unavailable')}</span>
                                        : p.source.kind === 'playlist' ? <LinkPick playlistId={p.source.id} songId={row.songId} /> : undefined}
                                    onOpen={() => open(row)} />
                            );
                        })}
                    {list.hasNextPage && <More onMore={() => { if (!list.isFetchingNextPage) void list.fetchNextPage({ cancelRefetch: false }); }} busy={list.isFetchingNextPage} />}
                    {searching && <SearchMore q={state.q} rows={rows} lit={lit} p={p} href={href} />}
                </div>
            )}
            {p.children}
            {p.source.kind === 'playlist' && <LinkBar playlistId={p.source.id} />}
        </div>
    );
}

function rowHref(row: Row, p: SongListProps, href: (path: string) => string): string {
    if (row.songId && row.playlistId) return href(p.songHref(row.playlistId, row.songId));
    return href(p.chordsHref(row.chords!.id));
}

/** The next page when the end of the list comes near (and a button, for keyboards and the sure). */
function More({ onMore, busy }: { onMore: () => void; busy: boolean }) {
    const { t } = useTranslation();
    const sentinel = useRef<HTMLDivElement>(null);
    const near = useRef(false);
    const more = useRef(onMore);
    more.current = onMore;
    // one observer for the list's life: it tells whether the end is near
    useEffect(() => {
        const element = sentinel.current;
        if (!element) return;
        const observer = new IntersectionObserver(entries => {
            near.current = entries.some(e => e.isIntersecting);
            if (near.current) more.current();
        }, { root: scrollerOf(element), rootMargin: '800px 0px' });
        observer.observe(element);
        return () => observer.disconnect();
    }, []);
    // a page came and the end is still near (a tall screen, a quick scroll): the next one
    useEffect(() => {
        if (!busy && near.current) more.current();
    }, [busy]);
    return (
        <div ref={sentinel} className="more-rows">
            <button type="button" className="btn sm" disabled={busy} onClick={onMore}>{busy ? t('common.loading') : t('common.showMore')}</button>
        </div>
    );
}

/** "By artist, compact": an artist, and their titles on one line. */
function Compact({ rows, q, lit, href, onOpen }: { rows: Row[]; q: string; lit: string | null; href: (row: Row) => string; onOpen: (row: Row) => void }) {
    const markSign = useMarkSign();
    const groups = useMemo(() => {
        const map = new Map<string, Row[]>();
        rows.forEach(row => {
            const artist = row.artists.join(', ');
            if (!map.has(artist)) map.set(artist, []);
            map.get(artist)!.push(row);
        });
        return [...map];
    }, [rows]);
    return (
        <>
            {groups.map(([artist, songs]) => (
                <Fragment key={artist}>
                    <div className="grp"><Highlight text={artist} q={q} /></div>
                    <div className="cmp">
                        {songs.map(row => (
                            <Link key={rowId(row)} to={href(row)} className={lit === rowId(row) ? 'last' : ''} onClick={() => onOpen(row)}>
                                <Highlight text={row.title} q={q} /> {row.myMarks.map(markSign).join('')}
                            </Link>
                        ))}
                    </div>
                </Fragment>
            ))}
        </>
    );
}

/** While searching, under the list: the songs of my other playlists, the music services' artists and recordings, and
 * chords from the sources (the mock-up's order). */
function SearchMore({ q, rows, lit, p, href }: { q: string; rows: Row[]; lit: string | null; p: SongListProps; href: (path: string) => string }) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const search = useSearch(q, ['songs', 'chords']);
    const shown = useMemo(() => new Set(rows.map(row => row.songId).filter(Boolean)), [rows]);
    const shownChords = useMemo(() => new Set(rows.map(row => row.chords?.id).filter(Boolean)), [rows]);
    if (!search.data) return search.isError ? <p className="failed">{errorText(search.error)}</p> : <p className="loading">{t('common.loading')}</p>;
    const songs = (search.data.songs ?? []).filter(song => !shown.has(song.songId) && !(p.source.kind === 'playlist' && song.playlistId === p.source.id));
    const chords = (search.data.chords ?? []).filter(header => !shownChords.has(header.id));
    return (
        <>
            {songs.length > 0 && <div className="sec">{t(!signedIn ? 'library.foundInOpen' : p.source.kind === 'playlist' ? 'library.otherPlaylists' : 'library.foundInPlaylists')}</div>}
            {songs.map(song => (
                <SongRow noMarks key={'s' + song.songId} to={href(`/playlists/${song.playlistId}/songs/${song.songId}`)} lit={lit === song.songId} q={q}
                    title={song.title} artists={song.artists}
                    extra={<span className="in">{song.playlistTitle}</span>} />
            ))}
            <MusicSearch q={q} />
            <div className="sec"><Icon name="globe" />{t('library.fromSources')} · {chords.length}</div>
            {chords.map(header => (
                <SongRow noMarks key={'c' + header.id} to={href(p.chordsHref(header.id))} lit={lit === header.id} q={q}
                    title={header.title ?? ''} artists={header.artists}
                    sources={[header.source]} />
            ))}
            {chords.length === 0 && <p className="empty">{t('library.emptySearch')}</p>}
            {!!search.data.chordsFailures?.length && (
                <p className="muted small" style={{ padding: '6px 8px' }}>
                    {t('library.sourcesFailed')}{' '}
                    <button type="button" className="lnk" disabled={search.isFetching} onClick={() => void search.refetch()}>{t('common.retry')}</button>
                </p>
            )}
        </>
    );
}

function SortChip({ sort, sorts, onSort }: { sort: ListSort; sorts: ListSort[]; onSort: (sort: ListSort) => void }) {
    const { t } = useTranslation();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const label = (s: ListSort) => t(sortLabels[s][0] as 'library.sort.popular');
    const full = (s: ListSort) => t(sortLabels[s][1] as 'library.sort.popular');
    return (
        <>
            <Chip onClick={e => setAnchor(anchor ? null : e.currentTarget)} title={full(sort)}>{label(sort)}<Icon name="down" /></Chip>
            <Popover anchor={anchor} onClose={() => setAnchor(null)}>
                {sorts.map(s => <MenuItem key={s} on={s === sort} onClick={() => { onSort(s); setAnchor(null); }}>{full(s)}</MenuItem>)}
            </Popover>
        </>
    );
}

function WhoChip({ who, mark, people, onChange }: {
    who: string; mark: string | null; people: { id: string; name: string }[]; onChange: (patch: { who?: string; mark?: string | null }) => void;
}) {
    const { t } = useTranslation();
    const markSign = useMarkSign();
    const markKinds = useMarkKinds();
    const { accountId } = useSession();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const others = people.filter(person => person.id !== accountId);
    const name = who === 'me' ? t('library.who.mineShort') : who === 'all' ? t('library.who.anyShort') : others.find(x => x.id === who)?.name ?? '';
    const label = who === 'me' && !mark ? t('library.who.marks') : `${mark ? markSign(mark) : '♥'} ${name}`;
    const close = () => setAnchor(null);
    return (
        <>
            <Chip on={who !== 'me' || !!mark} onClick={e => setAnchor(anchor ? null : e.currentTarget)}>{label}<Icon name="down" /></Chip>
            <Popover anchor={anchor} onClose={close} width={300}>
                <MenuTitle>{t('library.who.title')}</MenuTitle>
                <MenuItem on={who === 'me'} onClick={() => { onChange({ who: 'me' }); close(); }}>{t('library.who.mine')}</MenuItem>
                <MenuItem on={who === 'all'} onClick={() => { onChange({ who: 'all' }); close(); }}>{t('library.who.any')}</MenuItem>
                {others.map(person => (
                    <MenuItem key={person.id} on={who === person.id} sub={t('library.who.onlyOf', { name: person.name })}
                        onClick={() => { onChange({ who: person.id }); close(); }}>{person.name}</MenuItem>
                ))}
                <hr />
                <MenuTitle>{t('library.who.withMark')}</MenuTitle>
                <div className="emo">
                    {markKinds.map(({ key: kind, sign }) => (
                        <button type="button" key={kind} className={mark === kind ? 'on' : ''} aria-pressed={mark === kind}
                            onClick={() => { onChange({ mark: mark === kind ? null : kind }); close(); }}>{sign}</button>
                    ))}
                </div>
                {mark && <MenuItem onClick={() => { onChange({ mark: null }); close(); }}>{t('library.who.resetMark')}</MenuItem>}
            </Popover>
        </>
    );
}

