// A list of songs with its search and filters — the library's and a playlist's (and the home's search, as the
// library's): live search as one types (the cursor stays), the sort, whose marks / which mark; read in pages as it is
// scrolled; while searching, below the list: the songs of my other playlists and chords from the sources.

import { Fragment, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useSession } from '../../data/session';
import { rowId, useChordsSources, useSearch, useSongList, type SongRow as Row, type SongSource } from '../../data/songs';
import { useMarkKinds, useMarkSign } from '../../data/marks';
import { errorText } from '../../api/errors';
import { Chip, SearchField } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { MenuItem, MenuTitle, Popover } from '../../kit/Popover';
import { SongRow, Highlight, SourceIcon } from '../../kit/SongRow';
import { ChordsMarks } from '../../kit/ChordsMarks';
import { scrollerOf } from '../../app/layout';
import { setLastOpened, useLastOpened } from '../../app/lastOpened';
import { myMarksOn, useLocalView } from '../../local';
import { serverParams, useListState, type ListSort } from './listParams';
import { foundArtists, foundSongs, foundSorts, isCompact, type FoundSort } from './found';
import { LinkBar, LinkPick } from '../song/linking';
import { MusicSearch } from '../music/MusicSearch';
import { atTheTop, updateSong } from '../../data/song';
import { notOnDevice } from '../../offline';
import { personName } from '../../kit/person';

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
    /** the songs may be moved (a playlist the person administers): in its own order, unfiltered, each row moves */
    movable?: boolean;
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
    // moving shows the order as it is: in the playlist's order, nothing searched or filtered
    const moving = !!p.movable && p.source.kind === 'playlist' && sort === 'order' && !searching && state.who === 'me' && !state.mark;
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
                    {list.isSuccess && rows.length === 0 && !searching && !state.mark && state.who === 'me' && <p className="empty">{t(p.source.kind === 'playlist' ? 'library.emptyPlaylist' : 'library.empty')}</p>}
                    {list.isSuccess && rows.length === 0 && !searching && (state.mark || state.who !== 'me') && <p className="empty">{t('library.empty')}</p>}
                    {p.searchOnly && list.isSuccess && rows.length > 0 && <p className="muted small" style={{ padding: '10px 8px 0', margin: 0 }}>{t('home.inMyPlaylists', { count: total })}</p>}
                    {sort === 'compact'
                        ? <Compact rows={rows} q={state.q} lit={lit} href={row => rowHref(row, p, href)} onOpen={open} />
                        : rows.map((row, i) => {
                            const id = rowId(row);
                            return (
                                <SongRow key={id} to={rowHref(row, p, href)} lit={lit === id} q={state.q} title={row.title} artists={row.artists}
                                    myMarks={myMarksOn(local, row.chordsIds) ?? row.myMarks} cover={row.cover} coverSlot={coverSlot} otherMarks={row.otherMarks} otherPeople={row.otherPeople}
                                    sources={row.sources} moreSources={Math.max(0, row.chordsCount - row.sources.length)}
                                    extra={notOnDevice(row) ? <span className="na muted">{t('offline.notKept')}</span>
                                        : row.chords && row.chords.state !== 'open' ? <span className="na muted">{t(row.chords.state === 'notOpen' ? 'library.notOpen' : 'library.unavailable')}</span>
                                        : p.source.kind === 'playlist' ? (
                                            <>
                                                <LinkPick playlistId={p.source.id} songId={row.songId} />
                                                {moving && row.songId && <Mover playlistId={p.source.id} songId={row.songId} up={i === 0 ? null : (rows[i - 2]?.songId ?? atTheTop)} down={rows[i + 1]?.songId ?? null} />}
                                            </>
                                        ) : undefined}
                                    onOpen={() => open(row)} />
                            );
                        })}
                    {list.hasNextPage && <More onMore={() => { if (!list.isFetchingNextPage) void list.fetchNextPage({ cancelRefetch: false }); }} busy={list.isFetchingNextPage} />}
                    {searching && <SearchMore q={state.q} found={state.found} onFound={found => update({ found })} rows={rows} lit={lit} p={p} href={href} />}
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
 * chords from the sources (the mock-up's order), grouped by song as the old shelf grouped them, in the order chosen. */
function SearchMore({ q, found, onFound, rows, lit, p, href }: {
    q: string; found: FoundSort; onFound: (found: FoundSort) => void; rows: Row[]; lit: string | null; p: SongListProps; href: (path: string) => string;
}) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const search = useSearch(q, ['songs', 'chords']);
    const shown = useMemo(() => new Set(rows.map(row => row.songId).filter(Boolean)), [rows]);
    const shownChords = useMemo(() => new Set(rows.map(row => row.chords?.id).filter(Boolean)), [rows]);
    if (!search.data) return search.isError ? <p className="failed">{errorText(search.error)}</p> : <p className="loading">{t('common.loading')}</p>;
    const songs = (search.data.songs ?? []).filter(song => !shown.has(song.songId) && !(p.source.kind === 'playlist' && song.playlistId === p.source.id));
    const chords = (search.data.chords ?? []).filter(header => !shownChords.has(header.id));
    const chordsSongs = foundSongs(chords, found);
    return (
        <>
            {songs.length > 0 && <div className="sec">{t(!signedIn ? 'library.foundInOpen' : p.source.kind === 'playlist' ? 'library.otherPlaylists' : 'library.foundInPlaylists')}</div>}
            {songs.map(song => (
                <SongRow noMarks key={'s' + song.songId} to={href(`/playlists/${song.playlistId}/songs/${song.songId}`)} lit={lit === song.songId} q={q}
                    title={song.title} artists={song.artists}
                    extra={<span className="in">{song.playlistTitle}</span>} />
            ))}
            <MusicSearch q={q} />
            {/* chords of the songs found above are not repeated here: with none besides them, no section saying "none" */}
            {(chords.length > 0 || (rows.length === 0 && songs.length === 0)) && (
                <>
                    <div className="sec"><Icon name="frets" />{t('library.fromSources')} · {chords.length}</div>
                    <SourcesRibbon />
                    {chordsSongs.length > 1 && <div className="filters"><FoundSortChip found={found} onFound={onFound} /></div>}
                </>
            )}
            {isCompact(found) && foundArtists(chordsSongs).map(group => (
                <Fragment key={'a' + group.artist}>
                    <div className="grp"><Highlight text={group.songs[0]!.versions[0]!.artists.join(', ')} q={q} /></div>
                    <div className="cmp">
                        {group.songs.map(song => {
                            const first = song.versions[0]!;
                            return (
                                <Link key={song.key} to={href(p.chordsHref(first.id))} className={song.versions.some(x => x.id === lit) ? 'last' : undefined}>
                                    <Highlight text={first.title ?? ''} q={q} />{song.versions.length > 1 && <small className="muted"> ×{song.versions.length}</small>}
                                </Link>
                            );
                        })}
                    </div>
                </Fragment>
            ))}
            {!isCompact(found) && chordsSongs.map(song => {
                const first = song.versions[0]!;
                if (song.versions.length === 1) {
                    return (
                        <SongRow noMarks key={'c' + first.id} to={href(p.chordsHref(first.id))} lit={lit === first.id} q={q}
                            title={first.title ?? ''} artists={first.artists}
                            extra={<ChordsMarks header={first} />}
                            sources={[first.source]} />
                    );
                }
                return (
                    <Fragment key={'g' + song.key}>
                        <SongRow noMarks to={href(p.chordsHref(first.id))} lit={false} q={q}
                            title={first.title ?? ''} artists={first.artists}
                            sources={[...new Map(song.versions.map(x => [x.source.key, x.source])).values()]} />
                        <div className="fver">
                            {song.versions.map(version => (
                                <Link key={version.id} to={href(p.chordsHref(version.id))} className={lit === version.id ? 'last' : undefined}>
                                    <SourceIcon source={version.source} />
                                    <span>{version.source.title}</span>
                                    <ChordsMarks header={version} />
                                </Link>
                            ))}
                        </div>
                    </Fragment>
                );
            })}
            {chords.length === 0 && rows.length === 0 && songs.length === 0 && <p className="empty">{t('library.emptySearch')}</p>}
            {!!search.data.chordsFailures?.length && (
                <p className="muted small" style={{ padding: '6px 8px' }}>
                    {t('library.sourcesFailed')}{' '}
                    <button type="button" className="lnk" disabled={search.isFetching} onClick={() => void search.refetch()}>{t('common.retry')}</button>
                </p>
            )}
        </>
    );
}

const foundLabels: Record<FoundSort, string> = {
    original: 'library.found.original', artist: 'library.found.artist', title: 'library.found.title',
    compact: 'library.found.compact', compactArtist: 'library.found.compactArtist',
};

/** The order of the chords found: the old shelf's five — as found, by artist, by title, and compact as found or by artist. */
function FoundSortChip({ found, onFound }: { found: FoundSort; onFound: (found: FoundSort) => void }) {
    const { t } = useTranslation();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const label = (s: FoundSort) => t(foundLabels[s] as 'library.found.original');
    return (
        <>
            <Chip onClick={e => setAnchor(anchor ? null : e.currentTarget)}>{label(found)}<Icon name="down" /></Chip>
            <Popover anchor={anchor} onClose={() => setAnchor(null)}>
                {foundSorts.map(s => <MenuItem key={s} on={s === found} onClick={() => { onFound(s); setAnchor(null); }}>{label(s)}</MenuItem>)}
            </Popover>
        </>
    );
}

/** Where the chords are looked for: the sources this person may see, by their icons and names. */
function SourcesRibbon() {
    const { t } = useTranslation();
    const sources = useChordsSources().data?.sources ?? [];
    if (!sources.length) return null;
    return (
        <p className="srcribbon">
            <span className="lab">{t('library.sources')}</span>
            {sources.map(source => <span key={source.key}>{source.iconUrl && <SourceIcon source={source} />}{source.title}</span>)}
        </p>
    );
}

/** Up and down by one in the playlist's order (`up`/`down`: the song to go after; null: it is at that end). */
function Mover({ playlistId, songId, up, down }: { playlistId: string; songId: string; up: string | null; down: string | null }) {
    const { t } = useTranslation();
    const move = (e: React.SyntheticEvent, after: string | null) => {
        e.preventDefault();
        e.stopPropagation();
        if (after) void updateSong({ playlistId, songId, after }).catch(() => undefined);
    };
    return (
        <span className="mover">
            <button type="button" disabled={!up} aria-label={t('song.moveUp')} title={t('song.moveUp')} onClick={e => move(e, up)}><Icon name="down" className="flip" /></button>
            <button type="button" disabled={!down} aria-label={t('song.moveDown')} title={t('song.moveDown')} onClick={e => move(e, down)}><Icon name="down" /></button>
        </span>
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
    who: string; mark: string | null; people: { id: string; name: string; gone?: boolean }[]; onChange: (patch: { who?: string; mark?: string | null }) => void;
}) {
    const { t } = useTranslation();
    const markSign = useMarkSign();
    const markKinds = useMarkKinds();
    const { accountId } = useSession();
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const others = people.filter(person => person.id !== accountId);
    const one = others.find(x => x.id === who);
    const name = who === 'me' ? t('library.who.mineShort') : who === 'all' ? t('library.who.anyShort') : one ? personName(one) : '';
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
                    <MenuItem key={person.id} on={who === person.id} sub={person.gone ? t('library.who.onlyOfGone') : t('library.who.onlyOf', { name: person.name })}
                        onClick={() => { onChange({ who: person.id }); close(); }}>{personName(person)}</MenuItem>
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

