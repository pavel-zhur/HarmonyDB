// The PDF book of a playlist (/book?playlist=<id>[&share=<token>]): the cover and the common options, the songs in the
// playlist's order — each in or out, its key, one or two columns, its variant, moved up and down — how the pages will
// lie (counted on the server: a job), and the book made (a job; its PDF is the job's file).

import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiError, type Schemas } from '../../api/client';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { countBookPages, jobFileUrl, makeBook, resultOf, type BookPagesResult } from '../../data/jobs';
import { playlistTitle, useJob, usePlaylist } from '../../data/playlists';
import { useSession } from '../../data/session';
import { useSettings } from '../../data/settings';
import { useSongList, type SongRow } from '../../data/songs';
import { useLocalView } from '../../local';
import { keyKey } from '../../local/state';
import { Seg, Switch } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { keyText, letter } from '../song/keys';
import './book.css';

type Accidentals = Schemas['BookAccidentals'];

interface Row {
    songId: string;
    title: string;
    artists: string[];
    chordsIds: string[];
    chords: string;
    on: boolean;
    /** null: my key for these chords (here, else mine, else as written) */
    transpose: number | null;
    twoColumns: boolean;
    number: number;
}

export function BookScreen() {
    const { t } = useTranslation();
    const [params] = useSearchParams();
    const playlistId = params.get('playlist');
    const share = params.get('share');
    const { session } = useSession();
    if (!playlistId) return <Page><h1>{t('playlist.book')}</h1><p className="muted">{t('book.from')}</p></Page>;
    if (session && !session.permissions.includes('pdf')) {
        return <Page><h1>{t('playlist.book')}</h1><p className="muted">{t('book.noPermission')}</p></Page>;
    }
    return <Page wide><Book key={playlistId} playlistId={playlistId} share={share} /></Page>;
}

/** All the playlist's songs, in its order (read in pages until the end). */
function useAllSongs(playlistId: string, share: string | null) {
    const list = useSongList({ kind: 'playlist', id: playlistId, share }, { q: '', sort: 'order', who: null, mark: null });
    useEffect(() => {
        if (list.hasNextPage && !list.isFetchingNextPage) void list.fetchNextPage();
    }, [list.hasNextPage, list.isFetchingNextPage, list.fetchNextPage]);
    const done = !!list.data && !list.hasNextPage;
    const rows = useMemo(() => (done ? list.data!.pages.flatMap(p => p.rows) : null), [done, list.data]);
    return { rows, error: list.error };
}

const toRow = (row: SongRow, i: number): Row | null => {
    const chords = row.chords?.id ?? row.chordsIds[0];
    if (!row.songId || !chords) return null;
    return { songId: row.songId, title: row.title, artists: row.artists, chordsIds: row.chordsIds, chords, on: true, transpose: null, twoColumns: false, number: i + 1 };
};

function Book({ playlistId, share }: { playlistId: string; share: string | null }) {
    const { t } = useTranslation();
    const playlist = usePlaylist(playlistId, share);
    const songs = useAllSongs(playlistId, share);
    const settings = useSettings();
    const [rows, setRows] = useState<Row[] | null>(null);
    const [caption, setCaption] = useState('');
    const [contents, setContents] = useState(true);
    const [renumber, setRenumber] = useState(true);
    const [accidentals, setAccidentals] = useState<Accidentals>(settings.accidentals);
    const [pagesJob, setPagesJob] = useState<string | null>(null);
    const [bookJob, setBookJob] = useState<string | null>(null);

    useEffect(() => {
        if (songs.rows && rows === null) setRows(songs.rows.map(toRow).filter((x): x is Row => x !== null));
    }, [songs.rows, rows]);

    const request = (list: Row[]): Schemas['BookRequest'] => ({
        items: list.filter(r => r.on).map(r => ({ chordsId: r.chords, transpose: r.transpose, twoColumns: r.twoColumns, number: r.number })),
        playlistId,
        options: { caption: caption.trim() || null, contents: !!caption.trim() && contents, renumber, accidentals },
    });

    const count = useMutation({ mutationFn: () => countBookPages(request(rows ?? [])), onSuccess: job => setPagesJob(job.id) });
    const make = useMutation({ mutationFn: () => makeBook(request(rows ?? [])), onSuccess: job => setBookJob(job.id) });
    const pages = useJob(pagesJob);
    const book = useJob(bookJob);
    const counted = resultOf(pages.data, 'book_pages');
    const made = resultOf(book.data, 'book');

    // what changes the book makes the counted pages and the made book stale
    const change = (next: Row[]) => {
        setRows(next);
        setBookJob(null);
    };
    useEffect(() => setBookJob(null), [caption, contents, renumber, accidentals]);
    // the contents' pages are counted with the songs: a contents switched on or off is counted anew
    useEffect(() => setPagesJob(null), [!!caption.trim() && contents]);

    const error = playlist.error ?? songs.error;
    if (error) return <p className="failed">{errorText(error)}</p>;
    if (!rows || !playlist.data) return <p className="loading">{t('common.loading')}</p>;

    const chosen = rows.filter(r => r.on).length;
    const pagesOf = (r: Row) => {
        const item = counted?.items.find(x => x.chordsId === r.chords);
        return item ? (r.twoColumns ? item.pagesTwoColumns : item.pagesOneColumn) ?? null : null;
    };
    const update = (i: number, patch: Partial<Row>) => change(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
    const move = (i: number, d: -1 | 1) => {
        const next = rows.slice();
        [next[i], next[i + d]] = [next[i + d]!, next[i]!];
        change(next);
    };
    // two columns where they take a page less
    const savePages = () => change(rows.map(r => {
        const item = counted?.items.find(x => x.chordsId === r.chords);
        return item?.pagesOneColumn && item.pagesTwoColumns && item.pagesTwoColumns < item.pagesOneColumn ? { ...r, twoColumns: true } : r;
    }));

    return (
        <div className="form bk-form">
            <p><Link className="btn" to={`/playlists/${playlistId}${share ? `?share=${encodeURIComponent(share)}` : ''}`}><Icon name="left" />{t('book.back')}</Link></p>
            <h1>{t('playlist.book')}</h1>
            <p className="lead">{t('book.lead', { title: playlistTitle(playlist.data.playlist), chosen, count: rows.length })}</p>

            <section>
                <span className="lab">{t('book.cover')}</span>
                <div className="lines">
                    <div className="setrow">
                        <span>{t('book.caption')}<span className="sub">{t('book.captionSub')}</span></span>
                        <input className="field bk-cap" value={caption} onChange={e => setCaption(e.target.value)} placeholder={t('book.captionPlaceholder')} aria-label={t('book.caption')} />
                    </div>
                    <div className="setrow">
                        <span>{t('book.contents')}{!caption.trim() && <span className="sub">{t('book.contentsSub')}</span>}</span>
                        <Switch on={!!caption.trim() && contents} disabled={!caption.trim()} label={t('book.contents')} onChange={setContents} />
                    </div>
                    <div className="setrow">
                        <span>{t('book.renumber')}<span className="sub">{t('book.renumberSub')}</span></span>
                        <Switch on={renumber} label={t('book.renumber')} onChange={setRenumber} />
                    </div>
                    <div className="setrow">
                        <span>{t('book.accidentals')}</span>
                        <Seg value={accidentals} options={[['flats', '♭'], ['asWritten', t('book.asWritten')], ['sharps', '♯']]} onChange={setAccidentals} />
                    </div>
                </div>
            </section>

            <section>
                <span className="lab">{t('book.songs')}</span>
                <div className="chips bk-chips">
                    <button type="button" className="chip" onClick={() => change(rows.map(r => ({ ...r, on: true })))}>{t('book.all')}</button>
                    <button type="button" className="chip" onClick={() => change(rows.map(r => ({ ...r, on: false })))}>{t('book.none')}</button>
                    <button type="button" className="chip" disabled={!counted} title={t('book.savePagesSub')} onClick={savePages}><Icon name="file" />{t('book.savePages')}</button>
                    <button type="button" className="chip" onClick={() => change(songs.rows!.map(toRow).filter((x): x is Row => x !== null))}>{t('book.reset')}</button>
                </div>
                <div className="bk-list">
                    {rows.map((r, i) => (
                        <BookRow key={r.songId} row={r} index={i} last={i === rows.length - 1} playlistId={playlistId} pages={pagesOf(r)}
                            counted={counted?.items.find(x => x.chordsId === r.chords) ?? null} shownNumber={renumber ? rows.slice(0, i + 1).filter(x => x.on).length : r.number}
                            onChange={patch => update(i, patch)} onMove={d => move(i, d)} />
                    ))}
                </div>
            </section>

            <section>
                <span className="lab">{t('book.spreads')}</span>
                {counted
                    ? <Spreads rows={rows} counted={counted} caption={!!caption.trim()} contents={!!caption.trim() && contents} renumber={renumber} />
                    : <p className="muted small">{t('book.spreadsFirst')}</p>}
                <p><button type="button" className="btn" disabled={!chosen || count.isPending || (pages.data?.status === 'queued' || pages.data?.status === 'running')} onClick={() => count.mutate()}>
                    <Icon name="loop" className={pages.data?.status === 'running' || pages.data?.status === 'queued' ? 'spin' : undefined} />{t(counted ? 'book.countAgain' : 'book.count')}
                </button></p>
                {pages.data?.status === 'failed' && <p className="failed">{pages.data.errorCode ? errorText(new ApiError(0, pages.data.errorCode)) : t('book.failed')}</p>}
            </section>

            <div className="bk-sum">
                {made && <p><b>{t('book.pages', { count: made.pages })}</b> · {t('book.sheets', { count: Math.ceil(made.pages / 2) })}</p>}
                {made && made.items.some(x => x.state !== 'printed') && <p className="muted small">{t('book.leftOut', { count: made.items.filter(x => x.state !== 'printed').length })}</p>}
                {book.data && (book.data.status === 'queued' || book.data.status === 'running') && (
                    <p className="bk-busy"><Icon name="loop" className="spin" />{t(`book.stage.${book.data.stage ?? 'reading'}` as 'book.stage.reading')}{book.data.progress != null && ` · ${Math.round(book.data.progress * 100)}%`}</p>
                )}
                {book.data?.status === 'failed' && <p className="failed">{book.data.errorCode ? errorText(new ApiError(0, book.data.errorCode)) : t('book.failed')}</p>}
                {(make.isError || count.isError) && <p className="failed">{errorText(make.error ?? count.error)}</p>}
                <div className="chips">
                    {made && book.data && <a className="btn pri" href={jobFileUrl(book.data.id)} download><Icon name="imp" />{t('book.download')}</a>}
                    <button type="button" className={made ? 'btn' : 'btn pri'} disabled={!chosen || make.isPending || (!!book.data && !made && book.data.status !== 'failed')} onClick={() => make.mutate()}>
                        <Icon name="file" />{t(made ? 'book.makeAgain' : 'book.make')}
                    </button>
                </div>
            </div>
        </div>
    );
}

function BookRow({ row, index, last, playlistId, pages, counted, shownNumber, onChange, onMove }: {
    row: Row; index: number; last: boolean; playlistId: string; pages: number | null; counted: BookPagesResult['items'][number] | null; shownNumber: number;
    onChange: (patch: Partial<Row>) => void; onMove: (d: -1 | 1) => void;
}) {
    const { t } = useTranslation();
    const view = useLocalView();
    // my key for these chords, as the server will take it when none is set here
    const myKey = view.keys[keyKey(row.chords, playlistId)]?.semitones ?? view.keys[keyKey(row.chords, null)]?.semitones ?? 0;
    const transpose = row.transpose ?? myKey;
    const step = (d: number) => {
        let next = transpose + d;
        if (next > 11) next -= 12;
        if (next < -11) next += 12;
        onChange({ transpose: next });
    };
    const variant = row.chordsIds.indexOf(row.chords);
    const nextVariant = () => onChange({ chords: row.chordsIds[(variant + 1) % row.chordsIds.length]!, transpose: null });
    const one = counted?.pagesOneColumn, two = counted?.pagesTwoColumns;
    return (
        <div className={row.on ? 'bk-row' : 'bk-row off'}>
            <button type="button" className={row.on ? 'bk-ck on' : 'bk-ck'} role="checkbox" aria-checked={row.on} aria-label={t('book.include')} onClick={() => onChange({ on: !row.on })}>
                {row.on && <Icon name="check" />}
            </button>
            <span className="bk-t">
                <b>{row.on && `${shownNumber}. `}{row.title}</b>
                <span className="muted small">{row.artists.join(', ')}{row.chordsIds.length > 1 && ` · ${t('book.variant', { letter: letter(variant) })}`}</span>
            </span>
            <span className="bk-pg">{row.on && pages !== null ? t('book.pagesShort', { count: pages }) : ''}</span>
            <span className="bk-ord">
                <button type="button" aria-label={t('book.up')} disabled={index === 0} onClick={() => onMove(-1)}><Icon name="up" /></button>
                <button type="button" aria-label={t('book.down')} disabled={last} onClick={() => onMove(1)}><Icon name="down" /></button>
            </span>
            {row.on && (
                <div className="bk-opts">
                    <span className="step">
                        <button type="button" aria-label={t('book.keyDown')} onClick={() => step(-1)}>−</button>
                        <span className="v" title={row.transpose === null ? t('book.myKey') : undefined}>{keyText(transpose)}</span>
                        <button type="button" aria-label={t('book.keyUp')} onClick={() => step(1)}>+</button>
                    </span>
                    <Seg value={row.twoColumns ? '2' : '1'} onChange={v => onChange({ twoColumns: v === '2' })}
                        options={[['1', t('book.oneColumn') + (one && two && one !== two ? ` · ${t('book.pagesShort', { count: one })}` : '')],
                            ['2', t('book.twoColumns') + (one && two && one !== two ? ` · ${t('book.pagesShort', { count: two })}` : '')]]} />
                    {row.chordsIds.length > 1 && <button type="button" className="chip" onClick={nextVariant}>{t('book.variant', { letter: letter(variant) })}<Icon name="right" /></button>}
                    {counted && counted.state !== 'printed' && <span className="failed small">{t(`book.state.${counted.state}`)}</span>}
                </div>
            )}
        </div>
    );
}

/**
 * How the pages lie, as pairs of pages (spreads): the cover and the contents (with a caption), each song its pages; a
 * song of more than one page starts on the left of a spread, an empty page before it where needed (the server's rule,
 * with a caption).
 */
function Spreads({ rows, counted, caption, contents, renumber }: { rows: Row[]; counted: BookPagesResult; caption: boolean; contents: boolean; renumber: boolean }) {
    const { t } = useTranslation();
    const pages: { kind: 'cover' | 'toc' | 'blank' | 'song'; n?: number }[] = [];
    if (caption) pages.push({ kind: 'cover' });
    if (contents) for (let i = 0; i < (counted.contentsPages ?? 0); i++) pages.push({ kind: 'toc' });
    let n = 0, blanks = 0;
    for (const r of rows) {
        if (!r.on) continue;
        const item = counted.items.find(x => x.chordsId === r.chords);
        if (!item || item.state !== 'printed') continue;
        n++;
        const count = (r.twoColumns ? item.pagesTwoColumns : item.pagesOneColumn) ?? 1;
        if (caption && count > 1 && pages.length % 2 === 0) {
            pages.push({ kind: 'blank' });
            blanks++;
        }
        for (let i = 0; i < count; i++) pages.push({ kind: 'song', n: renumber ? n : r.number });
    }
    // the first page alone on the right, then pairs
    const spreads: (typeof pages[number] | null)[][] = [[null, pages[0] ?? null]];
    for (let i = 1; i < pages.length; i += 2) spreads.push([pages[i]!, pages[i + 1] ?? null]);
    const page = (p: typeof pages[number] | null, i: number) => p
        ? <i key={i} className={`bk-p ${p.kind}`}>{p.kind === 'cover' ? t('book.coverShort') : p.kind === 'toc' ? t('book.tocShort') : p.kind === 'song' ? p.n : ''}</i>
        : <i key={i} className="bk-p none" />;
    return (
        <>
            <div className="bk-spreads">{spreads.map((s, i) => <span className="bk-spr" key={i}>{s.map(page)}</span>)}</div>
            <p className="muted small">{t('book.spreadsNote')}</p>
            <p><b>{t('book.pages', { count: pages.length })}</b> · {t('book.sheets', { count: Math.ceil(pages.length / 2) })}{blanks > 0 && ` · ${t('book.blanks', { count: blanks })}`}</p>
        </>
    );
}
