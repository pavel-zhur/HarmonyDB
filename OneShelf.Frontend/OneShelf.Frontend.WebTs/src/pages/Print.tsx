// The original: Pages/Print.razor.
//
// The page's fields live in one object (as the component instance's fields); _preferences is the object Preferences
// keeps, changed in place as there. The result rows are the C# value tuples as objects with their element names.
// As in the original, the page follows the collection index (OnNewCollectionIndexReceived) only from when its
// preferences have been read (base.OnInitializedAsync subscribes after that).
// Whitespace text between elements, where the Razor markup keeps it, is a single space here.

import { Fragment, useEffect, useRef } from 'react';
import { listSet } from '../dotnet/collections';
import { ZipArchive } from '../dotnet/zipArchive';
import { blazorChecked, blazorValue } from '../framework/attributes';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { combineRefs, nativeChange, type NativeChangeEvent } from '../framework/elementRefs';
import { getLogger } from '../framework/logging';
import { useQueryParameters } from '../framework/supplyParameterFromQuery';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { DotNetStreamReference, invokeWindowFunction } from '../interop/jsRuntime';
import { GetPdfsRequestVersion } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/GetPdfsRequestVersion';
import type { ISong } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Interfaces/ISong';
import type { Like } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Like';
import type { Version } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Version';
import { getRequired } from '../dotnet/collections';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { single } from '../lib/common/dotnet/Linq';
import { isNullOrWhiteSpace, parseInteger } from '../lib/common/dotnet/text';
import { NoteAlteration } from '../lib/common/HarmonyDB.Common/NoteAlteration';
import type { PrintPreferences } from '../models/PrintPreferences';
import { PrintPreferences as PrintPreferencesClass } from '../models/PrintPreferences';
import { services } from '../services/registry';
import './Print.scoped.scss';
import { errorHandledTask } from '../framework/unhandledExceptions';

interface Row {
    songId: number;
    artists: string;
    title: string;
    index: number;
    error: string | null;
    onePageCount: number | null;
    twoPageCount: number | null;
    previewLinkOne: string | null;
    previewLinkTwo: string | null;
    versionId: number | null;
    likeTransposition: number | null;
    externalId: string | null;
}

interface VersionToRequest {
    artists: string;
    title: string;
    song: ISong;
    version: Version;
    likeTranspose: number;
}

/** A member access on a null reference. */
function nonNull<T>(value: T | null | undefined): T {
    if (value === null || value === undefined) throw new TypeError('Object reference not set to an instance of an object.');
    return value;
}

/** `nullable.Value` */
function valueOf(value: number | null): number {
    if (value === null) throw new InvalidOperationException('Nullable object must have a value.');
    return value;
}

/** OrderBy(keySelector) on ints: stable. */
function orderBy<T>(source: readonly T[], key: (x: T) => number): T[] {
    return source.map((x, i) => ({ x, i, k: key(x) })).sort((a, b) => a.k - b.k || a.i - b.i).map(x => x.x);
}

/** Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct() of .NET in the browser (Unix): '\0' and '/'. */
const invalidFileNameChars = ['\0', '/'];

/** `value?.ToString()` of an int? in an interpolation: empty for null. */
function text(value: number | null): string {
    return value === null ? '' : String(value);
}

export function Print() {
    const api = services.api;
    const identityProvider = services.identityProvider;
    const logger = getLogger('OneShelf.Frontend.Web.Pages.Print');
    const preferences = services.preferences;

    const stateHasChanged = useStateHasChanged();
    const query = useQueryParameters();

    const f = useRef({
        indices: null as string | null,
        results: null as Row[] | null,
        isError: false,
        preferences: null as PrintPreferences | null,
        modified: false,
        generating: false,
        caption: '',
        includeInspiration: false,
        reindex: false,
        subscribed: false,
    }).current;

    // [SupplyParameterFromQuery] Indices
    f.indices = query.string('Indices');

    useCollectionComponent(async () => {
        if (!f.subscribed) return;
        await update();
    });

    const collectionIndex = () => services.collectionIndexProvider.peek().collectionIndex;

    const transposition = (value: number | null) => (value ?? 0) === 0 ? '0' : value! < 0 ? String(value).replace('-', '−') : `+${value}`;

    // OnInitializedAsync
    useEffect(() => {
        void errorHandledTask((async () => {
            f.preferences = await preferences.getPrintPreferences();
            f.subscribed = true;
            await update();
        })().then(() => stateHasChanged()));
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    async function update(): Promise<void> {
        if (f.modified) return;

        f.isError = false;
        f.results = null;
        stateHasChanged();
        try {
            const index = collectionIndex();
            if (index === null || isNullOrWhiteSpace(f.indices)) {
                console.log('Not initialized.');
                return;
            }

            console.log('Initializing...');
            const results: Row[] = [];
            let indices = f.indices!.split(',').map(x => parseInteger(x, -2147483648, 2147483647, 'Int32'));

            const prefs = f.preferences!;
            const sortedOrder = prefs.order === null ? null : [...prefs.order].sort((a, b) => a - b);
            const sortedIndices = [...indices].sort((a, b) => a - b);
            if (sortedOrder !== null && sortedOrder.length === sortedIndices.length && sortedOrder.every((x, i) => x === sortedIndices[i])) {
                indices = prefs.order!;
            } else {
                prefs.order = indices;
                await preferences.setPrintPreferences(prefs);
            }

            const versionsToRequest: VersionToRequest[] = [];
            const userId = () => identityProvider.requiredIdentity.id;

            for (const songIndex of indices) {
                stateHasChanged();
                const song = single(index.songsById.values(), x => x.index === songIndex);
                const artists = song.artists.map(a => getRequired(index.artistsById, a).name).join(', ');

                const supportedVersions = song.versions.filter(x => x.externalId !== null);

                switch (supportedVersions.length) {
                    case 0:
                        results.push({ songId: song.id, artists, title: song.title, index: song.index, error: 'Нет аккордов.', onePageCount: null, twoPageCount: null, previewLinkOne: null, previewLinkTwo: null, versionId: null, likeTransposition: null, externalId: null });
                        continue;
                    case 1: {
                        const singleVersion = single(supportedVersions);
                        // OrderByDescending(x => x.Level).ThenByDescending(x => x.Transpose.HasValue ? 1 : 0)
                        const ordered = song.likes.map((x, i) => ({ x, i }))
                            .sort((a, b) => b.x.level - a.x.level || (b.x.transpose !== null ? 1 : 0) - (a.x.transpose !== null ? 1 : 0) || a.i - b.i)
                            .map(x => x.x);
                        const first = ordered.find(x => x.userId === userId() && x.versionId === singleVersion.id && x.transpose !== null);
                        versionsToRequest.push({ artists, title: song.title, song, version: singleVersion, likeTranspose: first?.transpose ?? 0 });
                        continue;
                    }
                    default: {
                        const mine = song.likes.filter(x => x.userId === userId() && x.versionId !== null && supportedVersions.some(u => u.id === x.versionId));
                        // GroupBy(x => x.Level).MaxBy(x => x.Key)
                        let candidates: Like[] = [];
                        if (mine.length > 0) {
                            const levels: number[] = [];
                            for (const like of mine) if (!levels.includes(like.level)) levels.push(like.level);
                            let maxLevel = levels[0]!;
                            for (const level of levels) if (level > maxLevel) maxLevel = level;
                            candidates = mine.filter(x => x.level === maxLevel);
                        }

                        switch (candidates.length) {
                            case 0:
                                results.push({ songId: song.id, artists, title: song.title, index: song.index, error: 'Несколько аккордов и нет лайков.', onePageCount: null, twoPageCount: null, previewLinkOne: null, previewLinkTwo: null, versionId: null, likeTransposition: null, externalId: null });
                                continue;
                            case 1:
                                versionsToRequest.push({ artists, title: song.title, song, version: single(song.versions, x => single(candidates).versionId === x.id), likeTranspose: single(candidates).transpose ?? 0 });
                                continue;
                            default:
                                results.push({ songId: song.id, artists, title: song.title, index: song.index, error: 'Несколько лайков максимальной красноты.', onePageCount: null, twoPageCount: null, previewLinkOne: null, previewLinkTwo: null, versionId: null, likeTransposition: null, externalId: null });
                                continue;
                        }
                    }
                }
            }

            const pdfsOne = await api.getPdfs(false, versionsToRequest.map(x => new GetPdfsRequestVersion({
                versionId: x.version.id,
                alteration: prefs.alteration,
                transpose: prefs.transpositions.has(x.version.id) ? prefs.transpositions.get(x.version.id)! : x.likeTranspose,
                twoColumns: false,
            })));

            const pdfsTwo = await api.getPdfs(false, versionsToRequest.map(x => new GetPdfsRequestVersion({
                versionId: x.version.id,
                alteration: prefs.alteration,
                transpose: prefs.transpositions.has(x.version.id) ? prefs.transpositions.get(x.version.id)! : x.likeTranspose,
                twoColumns: true,
            })));

            for (const x of versionsToRequest) {
                const pdfOne = nonNull(pdfsOne.pdfs).get(x.version.externalId!) ?? null;
                const pdfTwo = nonNull(pdfsTwo.pdfs).get(x.version.externalId!) ?? null;
                results.push({
                    songId: x.song.id,
                    artists: x.artists,
                    title: x.title,
                    index: x.song.index,
                    error: pdfOne === null || pdfTwo === null ? 'Не удалось получить аккорды.' : null,
                    onePageCount: pdfOne?.pageCount ?? null,
                    twoPageCount: pdfTwo?.pageCount ?? null,
                    previewLinkOne: pdfOne?.previewLink ?? null,
                    previewLinkTwo: pdfTwo?.previewLink ?? null,
                    versionId: pdfOne === null || pdfTwo === null ? null : x.version.id,
                    likeTransposition: x.likeTranspose,
                    externalId: x.version.externalId,
                });
            }

            f.results = results;
        } catch (e) {
            logger.logError(e, 'Error getting the print details.');
            f.isError = true;
            f.results = null;
        }

        stateHasChanged();
    }

    async function transpose(versionId: number, value: number): Promise<void> {
        f.preferences!.transpositions.set(versionId, value);
        f.modified = true;
        await preferences.setPrintPreferences(f.preferences!);
    }

    async function changeComment(value: string | null, versionId: number): Promise<void> {
        if (isNullOrWhiteSpace(value)) f.preferences!.comments.delete(versionId);
        else f.preferences!.comments.set(versionId, value!);
        await preferences.setPrintPreferences(f.preferences!);
    }

    async function setTwoColumns(value: boolean, versionId: number): Promise<void> {
        if (value) f.preferences!.twoColumns.add(versionId);
        else f.preferences!.twoColumns.delete(versionId);
        await preferences.setPrintPreferences(f.preferences!);
    }

    async function setExclusion(value: boolean, songId: number): Promise<void> {
        if (!value) f.preferences!.exclusions.add(songId);
        else f.preferences!.exclusions.delete(songId);
        await preferences.setPrintPreferences(f.preferences!);
    }

    async function up(index: number): Promise<void> {
        const indices = nonNull(f.preferences!.order);
        const indexIndex = indices.indexOf(index);
        listSet(indices, indexIndex, indices[indexIndex - 1]!);
        listSet(indices, indexIndex - 1, index);
        await preferences.setPrintPreferences(f.preferences!);
    }

    async function down(index: number): Promise<void> {
        const indices = nonNull(f.preferences!.order);
        const indexIndex = indices.indexOf(index);
        listSet(indices, indexIndex, indices[indexIndex + 1]!);
        listSet(indices, indexIndex + 1, index);
        await preferences.setPrintPreferences(f.preferences!);
    }

    async function setAlteration(noteAlteration: NoteAlteration | null): Promise<void> {
        f.preferences!.alteration = noteAlteration;
        f.modified = true;
        await preferences.setPrintPreferences(f.preferences!);
    }

    async function refresh(): Promise<void> {
        f.modified = false;
        f.results = null;
        stateHasChanged();
        await update();
    }

    async function resetOrder(): Promise<void> {
        f.preferences!.order = null;
        f.results = null;
        stateHasChanged();
        await preferences.setPrintPreferences(f.preferences!);
        await update();
    }

    async function reset(): Promise<void> {
        f.preferences = new PrintPreferencesClass();
        f.results = null;
        stateHasChanged();
        await preferences.setPrintPreferences(f.preferences);
        await update();
    }

    async function generate(): Promise<void> {
        try {
            f.generating = true;
            const prefs = f.preferences!;
            const order = prefs.order;
            const results = orderBy(nonNull(f.results).filter(x => !prefs.exclusions.has(x.songId)), x => nonNull(order).indexOf(x.index));

            const pdfs = await api.getPdfs(true,
                results.map(x => new GetPdfsRequestVersion({
                    versionId: valueOf(x.versionId),
                    alteration: prefs.alteration,
                    transpose: prefs.transpositions.get(valueOf(x.versionId)) ?? 0,
                    twoColumns: prefs.twoColumns.has(valueOf(x.versionId)),
                })),
                f.includeInspiration,
                isNullOrWhiteSpace(f.caption) ? null : f.caption,
                f.reindex);

            const archive = new ZipArchive();
            for (const x of results) {
                const pdf = getRequired(nonNull(pdfs.pdfs), x.externalId!);
                const name = invalidFileNameChars.reduce((n, c) => n.split(c).join('_'), `${x.index} (i) ${x.artists} - ${x.title}.pdf`);
                await archive.addEntry(name, nonNull(pdf.pdfData));
            }

            await downloadFileFromStream(archive.toBytes(), 'book.zip');

            if (pdfs.volume !== null) {
                await downloadFileFromStream(pdfs.volume, 'book.pdf');
            }
        } finally {
            f.generating = false;
        }
    }

    async function downloadFileFromStream(data: Uint8Array, fileName: string): Promise<void> {
        await invokeWindowFunction<void>('downloadFileFromStream', fileName, new DotNetStreamReference(new Blob([data])));
    }

    const on = <TArgs extends unknown[]>(handler: (...args: TArgs) => void | Promise<unknown>) => blazorEventHandler(stateHasChanged, handler);

    function renderBody() {
        if (f.generating) {
            return <span>Генерация книжки...</span>;
        }

        if (f.isError) {
            return (
                <>
                    <span>Генерация в процессе. В связи с техническими особенностями, это нормально :) Пожалуйста, жмите на повтор. После второго-десятого раза всё получится. Прошу прощения за неудобный интерфейс. Если после двадцати непрерывных повторов в течение пяти-десяти минут никак не удаётся заставить это работать, то это баг, <a href="https://t.me/pavel_zhur">напишите мне об этом, пожалуйста, и я его починю</a>.</span>{' '}
                    <button className="btn btn-outline-secondary" onClick={on(update)}>Повтор</button>
                </>
            );
        }

        if (f.results === null) {
            return <span>Загрузка...</span>;
        }

        const results = f.results;
        const prefs = f.preferences!;

        let leftFor1 = false;
        let totalCount = 0;
        let emptyCount = 0;

        const rows = orderBy(results, x => prefs.order?.indexOf(x.index) ?? 0).map((row, i) => {
            const { songId, artists, title, index, error, onePageCount, twoPageCount, previewLinkOne, previewLinkTwo, versionId, likeTransposition } = row;
            const pageCount = prefs.twoColumns.has(versionId ?? 0) ? twoPageCount : onePageCount;
            const previewLink = prefs.twoColumns.has(versionId ?? 0) ? previewLinkTwo : previewLinkOne;
            const excluded = prefs.exclusions.has(songId);

            let empty = null;
            if (!excluded) {
                if (pageCount !== null && pageCount > 1 && leftFor1) {
                    emptyCount++;
                    totalCount++;
                    empty = (
                        <tr>
                            <td colSpan={7} className="empty">Пустая страница чтоб следующая песня не прерывалась</td>
                        </tr>
                    );
                }

                if (pageCount !== null) {
                    leftFor1 = pageCount % 2 === 1 && !(leftFor1 && pageCount === 1);
                    totalCount += pageCount;
                }
            }

            let pages;
            if (error === null) {
                const plural = pageCount === 1 ? 'страница' : pageCount !== null && pageCount >= 5 ? 'страниц' : 'страницы';
                if (excluded) {
                    pages = <td className="gray">{text(pageCount)} {plural}</td>;
                } else if (f.modified) {
                    pages = <td className="success">{text(pageCount)} {plural}</td>;
                } else {
                    pages = <td className="success"><a className="success" target="_new" href={(f.modified ? null : previewLink) ?? undefined}>{text(pageCount)} {plural}</a></td>;
                }
            } else {
                pages = <td className={excluded ? 'gray' : 'error'}>{error}</td>;
            }

            let versionCells;
            if (versionId !== null && !excluded) {
                const value = prefs.transpositions.get(versionId);
                versionCells = (
                    <>
                        <td className="transpose">
                            {value !== undefined && value !== likeTransposition
                                ? (
                                    <>
                                        <span>{transposition(likeTransposition)} → {transposition(value)}</span>{' '}
                                        <button className="btn btn-outline-secondary" onClick={on(() => transpose(versionId, value - 1))}>−</button>{' '}
                                        <button className="btn btn-outline-secondary" onClick={on(() => transpose(versionId, value + 1))}>+</button>
                                    </>
                                )
                                : (
                                    <>
                                        <span>{transposition(likeTransposition)}</span>{' '}
                                        <button className="btn btn-outline-secondary" onClick={on(() => transpose(versionId, valueOf(likeTransposition) - 1))}>−</button>{' '}
                                        <button className="btn btn-outline-secondary" onClick={on(() => transpose(versionId, valueOf(likeTransposition) + 1))}>+</button>
                                    </>
                                )}
                        </td>{' '}
                        <td className={`with-checkbox ${onePageCount === twoPageCount ? '' : pageCount === Math.max(valueOf(onePageCount), valueOf(twoPageCount)) ? 'opportunity-unused' : 'opportunity-used'}`}>
                            <input type="checkbox" ref={combineRefs<HTMLInputElement>(blazorChecked(prefs.twoColumns.has(versionId) ? 'checked' : null), nativeChange<HTMLInputElement>(on((e: NativeChangeEvent<HTMLInputElement>) => setTwoColumns(e.currentTarget.checked, versionId))))} />{' '}
                            <a href={(f.modified ? null : previewLinkOne) ?? undefined} target="_new">{text(onePageCount)} стр</a> → <a href={(f.modified ? null : previewLinkTwo) ?? undefined} target="_new">{text(twoPageCount)} стр</a>
                        </td>{' '}
                        <td>
                            <input type="text" ref={combineRefs<HTMLInputElement>(
                                blazorValue(prefs.comments.get(versionId) ?? null),
                                nativeChange<HTMLInputElement>(on((e: NativeChangeEvent<HTMLInputElement>) => changeComment(e.currentTarget.value, versionId))),
                            )} />
                        </td>
                    </>
                );
            } else {
                versionCells = (
                    <>
                        <td></td>{' '}
                        <td></td>{' '}
                        <td></td>
                    </>
                );
            }

            return (
                <Fragment key={i}>
                    {empty}
                    <tr>
                        <td className="with-checkbox">
                            <input type="checkbox" ref={combineRefs<HTMLInputElement>(blazorChecked(!excluded ? 'checked' : null), nativeChange<HTMLInputElement>(on((e: NativeChangeEvent<HTMLInputElement>) => setExclusion(e.currentTarget.checked, songId))))} />
                        </td>{' '}
                        <td className={excluded ? 'gray' : undefined}>{index}. {artists} - {title}</td>
                        {pages}
                        {versionCells}
                        <td>
                            {i !== 0 && <button className="btn btn-outline-secondary" onClick={on(() => up(index))}>Вверх</button>}
                            {i !== results.length - 1 && <button className="btn btn-outline-secondary" onClick={on(() => down(index))}>Вниз</button>}
                        </td>
                    </tr>
                </Fragment>
            );
        });

        return (
            <>
                <button className="btn btn-outline-secondary" onClick={on(resetOrder)}>Сбросить порядок</button>{' '}
                <button className="btn btn-outline-secondary" onClick={on(reset)}>Сбросить всё</button>
                {f.modified
                    ? <button className="btn btn-outline-secondary" onClick={on(refresh)}>Обновить просмотр</button>
                    : results.every(x => x.onePageCount !== null || prefs.exclusions.has(x.songId)) && (
                        <>
                            <div>Если хотите готовую книжку, введите заголовок: <input type="text" ref={combineRefs<HTMLInputElement>(
                                blazorValue(f.caption),
                                nativeChange<HTMLInputElement>(on((e: NativeChangeEvent<HTMLInputElement>) => { f.caption = e.currentTarget.value; })),
                            )} /></div>{' '}
                            <div><input type="checkbox" ref={combineRefs<HTMLInputElement>(blazorChecked(f.includeInspiration), nativeChange<HTMLInputElement>(on((e: NativeChangeEvent<HTMLInputElement>) => { f.includeInspiration = e.currentTarget.checked; })))} id="includeInspiration" /><label htmlFor="includeInspiration">Включить содержание</label></div>{' '}
                            <div><input type="checkbox" ref={combineRefs<HTMLInputElement>(blazorChecked(f.reindex), nativeChange<HTMLInputElement>(on((e: NativeChangeEvent<HTMLInputElement>) => { f.reindex = e.currentTarget.checked; })))} id="reindex" /><label htmlFor="reindex">Перенумеровать с 1</label></div>{' '}
                            <button className="btn btn-outline-secondary" onClick={on(generate)}>Сгенерить книжку</button>
                        </>
                    )}
                <div>
                    <span>Люблю:</span>{' '}
                    <input type="radio" name="alteration" id="preferSharp" value={NoteAlteration[NoteAlteration.Sharp]} ref={combineRefs<HTMLInputElement>(blazorChecked(prefs.alteration === NoteAlteration.Sharp), nativeChange<HTMLInputElement>(on(() => setAlteration(NoteAlteration.Sharp))))} />{' '}
                    <label htmlFor="preferSharp">###</label>{' '}
                    <input type="radio" name="alteration" id="preferFlat" value={NoteAlteration[NoteAlteration.Flat]} ref={combineRefs<HTMLInputElement>(blazorChecked(prefs.alteration === NoteAlteration.Flat), nativeChange<HTMLInputElement>(on(() => setAlteration(NoteAlteration.Flat))))} />{' '}
                    <label htmlFor="preferFlat">bbb</label>{' '}
                    <input type="radio" name="alteration" id="preferNone" value="null" ref={combineRefs<HTMLInputElement>(blazorChecked(prefs.alteration === null), nativeChange<HTMLInputElement>(on(() => setAlteration(null))))} />{' '}
                    <label htmlFor="preferNone">Что есть, то есть</label>
                </div>{' '}
                <div>
                    <table>
                        <tr className="header">
                            <td>Включаем?</td>{' '}
                            <td>Название</td>{' '}
                            <td>Сколько страниц занимает</td>{' '}
                            <td>Трнсп</td>{' '}
                            <td>Две колонки?</td>{' '}
                            <td>Памятка<br />(не попадёт в книжку)</td>{' '}
                            <td>Порядок</td>
                        </tr>
                        {rows}
                    </table>
                </div>{' '}
                <h3>Итоги:</h3>{' '}
                <div>Всего страниц: {totalCount + 1}, листов: {Math.ceil(Math.fround((totalCount + 1) / 2))}, пустых страниц: {emptyCount}</div>
            </>
        );
    }

    return (
        <>
            <h3>Генерация книжки из списка</h3>
            {renderBody()}
        </>
    );
}
