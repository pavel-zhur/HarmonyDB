// The original: Pages/Collective.razor.
//
// The page's fields live in one object (as the component instance's fields). Its parameters come from the query
// string (CollectiveId, VersionId, action); OnParametersSetAsync runs when the page appears and each time the query
// string changes (as [SupplyParameterFromQuery] notifies the page), before the page is shown with the new values.
// Task.Run and the System.Timers.Timer's Elapsed run later, from the browser's task queue.
// Whitespace text between elements, where the Razor markup keeps it, is a single space here.

import { useEffect, useLayoutEffect, useRef, type FormEvent, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { getRequired } from '../dotnet/collections';
import { combineRefs, nativeChange, textareaText } from '../framework/elementRefs';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { inlineEventAttribute } from '../framework/inlineEventAttribute';
import { getLogger } from '../framework/logging';
import { navigationManager } from '../framework/navigation';
import { PageTitle } from '../framework/PageTitle';
import { QueryParameters } from '../framework/supplyParameterFromQuery';
import { blazorChecked, blazorValue } from '../framework/attributes';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { InsertResponse } from '../lib/api/OneShelf.Collectives.Api.Model/V2/InsertResponse';
import { Collective as CollectiveModel } from '../lib/api/OneShelf.Collectives.Api.Model/V2/Sub/Collective';
import type { CollectiveVersion } from '../lib/api/OneShelf.Collectives.Api.Model/V2/Sub/CollectiveVersion';
import { CollectiveVisibility } from '../lib/api/OneShelf.Collectives.Api.Model/V2/Sub/CollectiveVisibility';
import { enumParse } from '../lib/common/dotnet/Enum';
import { ArgumentOutOfRangeException, InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { newGuid, type Guid } from '../lib/common/dotnet/Guid';
import { single } from '../lib/common/dotnet/Linq';
import { isNullOrWhiteSpace, split, StringSplitOptions } from '../lib/common/dotnet/text';
import { RepresentationSettings } from '../lib/representations/RepresentationSettings';
import type { CollectionIndex } from '../models/CollectionIndex';
import { services } from '../services/registry';
import { SearchResultsLocalSongsMode } from '../shared/SearchResultsLocal';
import './Collective.scoped.scss';

enum ActionType {
    Add,
    Delete,
    Refresh,
    Update,
}

const DummyContent = '[Вступление]\r\n\r\nНапример такое вступление.\r\n> Am Dm Gsus4/D\r\n\r\n[Куплет]\r\n>F       A\r\nИдёт куплетик,\r\n>F           A\r\nКончится война.\r\n\r\n[Бридж]\r\n<Am> | <Dm> | <Am> | <Dm> x4';

/** Guid.TryParse: the D, N, B and P formats, surrounding white space ignored; the Guid as its lowercase D form. */
function tryParseGuid(value: string): Guid | null {
    const text = value.trim();
    const hex = '[0-9a-fA-F]';
    const d = `(${hex}{8})-(${hex}{4})-(${hex}{4})-(${hex}{4})-(${hex}{12})`;
    const patterns = [
        new RegExp(`^${d}$`),
        new RegExp(`^\\{${d}\\}$`),
        new RegExp(`^\\(${d}\\)$`),
        new RegExp(`^(${hex}{8})(${hex}{4})(${hex}{4})(${hex}{4})(${hex}{12})$`),
    ];
    for (const pattern of patterns) {
        const match = pattern.exec(text);
        if (match) return match.slice(1, 6).join('-').toLowerCase();
    }

    return null;
}

interface Parameters {
    collectiveId: Guid | null;
    versionId: number | null;
    actionString: string | null;
}

/** The [SupplyParameterFromQuery] parameters: a value that does not parse throws, as when Blazor sets them. */
function readParameters(search: string): Parameters {
    const query = new QueryParameters(search);
    const collectiveIdText = query.string('CollectiveId');
    let collectiveId: Guid | null = null;
    if (collectiveIdText !== null && collectiveIdText.length > 0) {
        collectiveId = tryParseGuid(collectiveIdText);
        if (collectiveId === null) {
            throw new InvalidOperationException(`Cannot parse the value '${collectiveIdText}' as type 'System.Nullable\`1[System.Guid]' for 'CollectiveId'.`);
        }
    }

    return {
        collectiveId,
        versionId: query.nullableInt('VersionId'),
        actionString: query.string('action'),
    };
}

/** `Enum.TryParse<ActionType>(ActionString, out var x) ? x : ActionType.Add` */
function parseAction(actionString: string | null): ActionType {
    if (actionString === null) return ActionType.Add;
    try {
        return enumParse<ActionType>(ActionType, actionString);
    } catch {
        return ActionType.Add;
    }
}

/** `nullable.Value` */
function valueOf(value: number | null): number {
    if (value === null) throw new InvalidOperationException('Nullable object must have a value.');
    return value;
}

/** `{n:000}` */
function format000(n: number): string {
    return n < 0 ? '-' + String(-n).padStart(3, '0') : String(n).padStart(3, '0');
}

export function Collective() {
    const api = services.api;
    const logger = getLogger('OneShelf.Frontend.Web.Pages.Collective');
    const collectionNavigation = services.collectionNavigation;
    const collectivesApiClient = services.collectivesApiClient;
    const identityProvider = services.identityProvider;
    const collectionIndexProvider = services.collectionIndexProvider;
    const dataProvider = services.dataProvider;
    const options = services.frontendOptions;

    const stateHasChanged = useStateHasChanged();
    const location = useLocation();

    const f = useRef({
        timer: undefined as ReturnType<typeof setTimeout> | undefined,
        timerDisposed: false,

        preview: '',
        content: null as string | null,
        currently: null as Guid | null,
        applying: null as string | null,

        collectiveFailed: false,
        collective: null as CollectiveVersion | null,
        navigateOnSuccess: null as (() => string) | null,

        newName: null as string | null,
        newAuthor: null as string | null,
        newAccess: null as CollectiveVisibility | null,
        refreshing: false,

        parameters: null as Parameters | null,
        parametersSearch: null as string | null,
    }).current;

    if (f.parameters === null) {
        f.parameters = readParameters(location.search);
        f.parametersSearch = location.search;
    }

    const parameters = f.parameters;
    const action = () => parseAction(f.parameters!.actionString);
    const collectionIndex = (): CollectionIndex | null => collectionIndexProvider.peek().collectionIndex;

    // OnNewCollectionIndexReceived
    const { collectionIndex: renderCollectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(async () => {
        if (f.navigateOnSuccess !== null) {
            const index = collectionIndexProvider.peek().collectionIndex;
            if (index !== null) {
                navigationManager.navigateTo(f.navigateOnSuccess(), false, true);
            } else {
                f.applying = null;
            }
        } else {
            const { versionId } = f.parameters!;
            if (action() === ActionType.Add && versionId !== null && collectionIndex() !== null && f.content === null) {
                try {
                    const chords = await dataProvider.getChords(getRequired(collectionIndex()!.versionsById, versionId).version.externalId!);
                    f.content = chords.output.asOneShelf(new RepresentationSettings());
                    timerElapsed();
                } catch (e) {
                    logger.logError(e, 'Error fetching the chords version {id}.', versionId);
                    f.collectiveFailed = true;
                }
            }
        }
    });

    /** `ended` is called when the body has run to its end (to tell a task that completed without awaiting). */
    async function onParametersSetAsync(ended: () => void = () => { }): Promise<void> {
        const { collectiveId, versionId } = f.parameters!;

        f.collective = null;
        f.navigateOnSuccess = null;
        f.newName = null;
        f.newAuthor = null;
        f.newAccess = null;
        f.collectiveFailed = false;
        f.applying = null;
        f.currently = null;
        f.content = action() === ActionType.Add && versionId === null ? DummyContent : null;
        f.preview = '';

        if (action() === ActionType.Add && versionId !== null && collectionIndex() !== null && f.content === null) {
            try {
                const chords = await dataProvider.getChords(getRequired(collectionIndex()!.versionsById, versionId).version.externalId!);
                f.content = chords.output.asOneShelf(new RepresentationSettings());
                timerElapsed();
            } catch (e) {
                logger.logError(e, 'Error fetching the chords version {id}.', versionId);
                f.collectiveFailed = true;
            }
        }

        try {
            if (collectiveId !== null) {
                f.collectiveFailed = false;
                const response = await collectivesApiClient.get(collectiveId, identityProvider.requiredIdentity);
                if (response == null) throw new Error(`The collective ${collectiveId} could not be found.`);
                f.collective = response.version;
            }
        } catch (e) {
            logger.logError(e, 'Error fetching the collective {id}.', collectiveId);
            f.collectiveFailed = true;
        }

        ended();
    }

    // What Blazor does around OnParametersSetAsync: a render once it has started, another when it completes.
    // (A task that completed without awaiting anything is not awaited: no second render.)
    function runOnParametersSetAsync(): void {
        let ended = false;
        const task = onParametersSetAsync(() => {
            ended = true;
        });
        stateHasChanged();
        if (!ended) {
            void errorHandledTask(task.then(() => stateHasChanged()));
        } else {
            void errorHandledTask(task);
        }
    }

    // OnInitializedAsync (the timer) and Dispose
    useEffect(() => () => {
        f.timerDisposed = true;
        clearTimeout(f.timer);
    }, [f]);

    // The parameters: when the page appears, and when the query string changes.
    const parametersSetFor = useRef<string | null>(null);
    useLayoutEffect(() => {
        if (parametersSetFor.current === location.search) return;
        if (parametersSetFor.current !== null) {
            f.parameters = readParameters(location.search);
            f.parametersSearch = location.search;
        }

        parametersSetFor.current = location.search;
        runOnParametersSetAsync();
    });

    // OnAfterRenderAsync
    // (Not after a render made before OnParametersSetAsync ran for the address: Blazor makes no such render; this one
    // is replaced before the browser paints.)
    const renderedBeforeParametersSet = parametersSetFor.current !== location.search;
    useEffect(() => {
        if (renderedBeforeParametersSet) return;
        if ((f.content === null && f.collective !== null || f.content === DummyContent) && f.currently === null) {
            f.content ??= f.collective?.collective.contents ?? null;
            f.refreshing = true;
            timerElapsed();
        }
    });

    function contentUpdated(content: string | null): void {
        if (content !== null) {
            f.content = content;
            // _timer.Stop(); _timer.Start();
            clearTimeout(f.timer);
            if (!f.timerDisposed) {
                f.timer = setTimeout(() => timerElapsed(), 300);
            }

            f.refreshing = true;
        }
    }

    function timerElapsed(): void {
        // Task.Run (its task is not observed: a failure goes unseen)
        setTimeout(() => {
            (async () => {
                try {
                    if (f.content === null) return;
                    const currently = newGuid();
                    f.currently = currently;
                    const html = await api.formatPreview(f.content);
                    if (f.currently === currently) {
                        f.refreshing = false;
                        f.preview = html.asHtml(new RepresentationSettings());
                        stateHasChanged();
                    }
                } catch (e) {
                    f.preview = 'Случилась ошибка.';
                    f.refreshing = false;
                    stateHasChanged();
                    logger.logError(e, 'Error getting the preview...');
                }
            })().catch(() => {
            });
        }, 0);
    }

    async function retry(): Promise<void> {
        retryCollectionIndex();
        await onParametersSetAsync();
    }

    function getTitle(): string {
        try {
            if (f.collective !== null) {
                return `${f.collective.collective.authors.join(', ')} — ${f.collective.collective.title}`;
            }

            const { versionId } = f.parameters!;
            if (versionId !== null) {
                const index = collectionIndex()!;
                const song = getRequired(index.versionsById, versionId).song;
                return `${song.artists.map(a => getRequired(index.artistsById, a).name).join(', ')} — ${getRequired(index.versionsById, versionId).song.title}`;
            }

            throw new InvalidOperationException();
        } catch (e) {
            logger.logError(e, 'Error getting the song title.');
            return '(песня не найдена)';
        }
    }

    function getAuthors(): string | null {
        try {
            if (f.collective !== null) {
                return f.collective.collective.authors.join(', ');
            }

            const { versionId } = f.parameters!;
            if (versionId !== null) {
                const index = collectionIndex()!;
                return getRequired(index.versionsById, versionId).song.artists.map(a => getRequired(index.artistsById, a).name).join(', ');
            }

            return null;
        } catch (e) {
            logger.logError(e, 'Error getting the song title.');
            return '';
        }
    }

    function getSongName(): string | null {
        try {
            if (f.collective !== null) {
                return f.collective.collective.title;
            }

            const { versionId } = f.parameters!;
            if (versionId !== null) {
                return getRequired(collectionIndex()!.versionsById, versionId).song.title;
            }

            return null;
        } catch (e) {
            logger.logError(e, 'Error getting the song title.');
            return '';
        }
    }

    async function deleteCollective(): Promise<void> {
        await executeAndGo(
            'Удаляем...',
            async () => {
                await collectivesApiClient.delete(identityProvider.requiredIdentity, valueOfGuid(f.parameters!.collectiveId));
            },
            () => collectionNavigation.getExternalMyCollectivesNavigationLink());
    }

    async function refresh(): Promise<void> {
        await executeAndGo(
            'Обновляем циферки...',
            async () => {
                await collectivesApiClient.update(identityProvider.requiredIdentity, valueOfGuid(f.parameters!.collectiveId), nonNull(f.collective).collective);
            },
            () => {
                const { version, song } = single(collectionIndex()!.versionsById.values(), x => x.version.collectiveId === f.parameters!.collectiveId);
                return collectionNavigation.getExternalNavigationLink(song.id, version.id, version.externalId, SearchResultsLocalSongsMode.WithMyCollectives);
            });
    }

    async function create(): Promise<void> {
        switch (action()) {
            case ActionType.Add: {
                let insertResponse: InsertResponse | null = null;
                await executeAndGo(
                    'Создаем...',
                    async () => {
                        const { versionId } = f.parameters!;
                        insertResponse = await collectivesApiClient.insert(
                            identityProvider.requiredIdentity,
                            new CollectiveModel({
                                title: f.newName ?? getRequired(collectionIndex()!.versionsById, valueOf(versionId)).song.title,
                                authors: (f.newAuthor === null ? null : split(f.newAuthor, [','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                    ?? getRequired(collectionIndex()!.versionsById, valueOf(versionId)).song.artists.map(a => getRequired(collectionIndex()!.artistsById, a).name),
                                contents: f.content!,
                                visibility: f.newAccess ?? CollectiveVisibility.Club,
                            }),
                            versionId);
                    },
                    () => {
                        const { version, song } = single(collectionIndex()!.versionsById.values(), x => x.version.collectiveId === insertResponse!.version.collectiveId);
                        return collectionNavigation.getExternalNavigationLink(song.id, version.id, version.externalId, SearchResultsLocalSongsMode.WithMyCollectives);
                    });
                break;
            }
            case ActionType.Update:
                await executeAndGo(
                    'Сохраняем изменения...',
                    async () => {
                        const collective = nonNull(f.collective);
                        await collectivesApiClient.update(
                            identityProvider.requiredIdentity,
                            collective.collectiveId,
                            new CollectiveModel({
                                title: f.newName ?? collective.collective.title,
                                authors: (f.newAuthor === null ? null : split(f.newAuthor, [','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) ?? collective.collective.authors,
                                contents: f.content ?? collective.collective.contents,
                                visibility: f.newAccess ?? collective.collective.visibility,
                            }));
                    },
                    () => {
                        const { version, song } = single(collectionIndex()!.versionsById.values(), x => x.version.collectiveId === f.parameters!.collectiveId);
                        return collectionNavigation.getExternalNavigationLink(song.id, version.id, version.externalId, SearchResultsLocalSongsMode.WithMyCollectives);
                    });
                break;
            case ActionType.Delete:
            case ActionType.Refresh:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    async function executeAndGo(title: string, actionToRun: () => Promise<void>, navigateOnSuccess: () => string): Promise<void> {
        f.applying = title;
        try {
            await actionToRun();
            f.navigateOnSuccess = navigateOnSuccess;
            await collectionIndexProvider.clear();
        } catch (e) {
            // {action} is the Func<Task> delegate, as .NET formats it.
            logger.logError(e, 'Error applying action {action} the collective {id}.', 'System.Func`1[System.Threading.Tasks.Task]', f.parameters!.collectiveId);
            f.applying = null;
            f.collectiveFailed = true;
            f.collective = null;
        }
    }

    function getSearchTag(): string {
        try {
            const searchTag = nonNull(f.collective).searchTag;
            return `${format000(Math.trunc(searchTag / 1000))} ${format000(searchTag % 1000)}`;
        } catch (e) {
            logger.logError(e, 'Error getting the search tag.');
            return '(неизвестные циферки)';
        }
    }

    function getAddUpdateError(): string | null {
        const { versionId } = f.parameters!;
        if (isNullOrWhiteSpace(f.newName) && f.collective === null && versionId === null
            || (f.collective !== null || versionId !== null) && f.newName !== null && isNullOrWhiteSpace(f.newName)) {
            return '...не хватает названия...';
        }

        const noNewAuthor = isNullOrWhiteSpace(f.newAuthor) || split(f.newAuthor!, [','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).length === 0;
        if (noNewAuthor && f.collective === null && versionId === null
            || (f.collective !== null || versionId !== null) && f.newAuthor !== null && noNewAuthor) {
            return '...не хватает автора...';
        }

        if (isNullOrWhiteSpace(f.content)) return '...не хватает аккордов...';

        return null;
    }

    function getPageTitle(): string {
        switch (action()) {
            case ActionType.Add: return f.collective !== null ? `${getTitle()} – новые аккорды` : 'Новая песня';
            case ActionType.Delete: return f.collective !== null ? `${getTitle()} – удаление` : 'Удаление';
            case ActionType.Refresh: return f.collective !== null ? `${getTitle()} – обновление циферок` : 'Обновление циферок';
            case ActionType.Update: return f.collective !== null ? `${getTitle()} – редактирование` : 'Редактирование';
            default: throw new ArgumentOutOfRangeException();
        }
    }

    const on = <TArgs extends unknown[]>(handler: (...args: TArgs) => void | Promise<unknown>) => blazorEventHandler(stateHasChanged, handler);
    const historyBack = inlineEventAttribute('onclick', 'history.back()');
    const loading = <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>;

    function renderBody() {
        if (f.applying !== null) {
            return (
                <>
                    <h3 className="text-success">{f.applying}</h3>{' '}
                    {loading}
                </>
            );
        }

        if (collectionIndexFailed || f.collectiveFailed) {
            return (
                <>
                    <p><em>Не получилось загрузить, может нету интернета, библиотеку.</em></p>{' '}
                    <button className="btn btn-outline-secondary" onClick={on(retry)}>Повтор</button>
                </>
            );
        }

        if (renderCollectionIndex === null || parameters.collectiveId !== null && f.collective === null || parameters.versionId !== null && f.content === null) {
            return loading;
        }

        const currentAction = action();

        let first;
        switch (currentAction) {
            case ActionType.Add:
                first = parameters.versionId !== null
                    ? <li><b>Вы добавляете новые аккорды к существующей песне.</b></li>
                    : <li><b>Вы добавляете новую песню и аккорды к ней.</b></li>;
                break;
            case ActionType.Delete:
                first = <li><b>Вы удаляете свои аккорды.</b></li>;
                break;
            case ActionType.Refresh:
                first = <li><b>Вы обновляете циферки {getSearchTag()}.</b></li>;
                break;
            case ActionType.Update:
                first = <li><b>Вы редактируете свои аккорды.</b></li>;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        let editing = null;
        switch (currentAction) {
            case ActionType.Add:
            case ActionType.Update:
                editing = (
                    <>
                        <li>Пока что удобнее это делать с компьютера.</li>{' '}
                        <li>Вы можете редактировать свои аккорды, или добавить свою версию существующей песни.</li>{' '}
                        <li>В будущем вы сможете откатить аккорды к одной из прошлой версий, если вы случайно внесли плохое изменение. Свои прошлые версии будете видеть только вы.</li>{' '}
                        <li>
                            {' Разметка: '}
                            <ul>
                                <li>{'Если поставить знак > в начале строки, вся строка будет состоять из аккордов.'}</li>{' '}
                                <li>Обрамление строки целиком в [ ] — жирная строка.</li>{' '}
                                <li>{'Аккорд внутри строки, где еще есть неаккорды обрамляем в < >, например <Asus2/G> вот так.'}</li>
                            </ul>
                        </li>
                    </>
                );
                break;
        }

        const pageHeader = (title: ReactNode) => (
            <div className="page-header bg-success bg-opacity-75 my-header my-content-addition mt-4">
                <div className="page-header-content d-block">
                    <div className="d-flex justify-content-between">
                        <div className="breadcrumb">
                            <h5 className="my-pt-25">{title}</h5>
                        </div>
                    </div>
                </div>
            </div>
        );

        let second;
        switch (currentAction) {
            case ActionType.Delete:
                second = (
                    <>
                        {pageHeader(<>Удаление: {getTitle()}</>)}
                        <div className="mt-1 mb-1">
                            <button className="btn btn-danger me-1 mt-3" onClick={on(deleteCollective)}>Да, удалить навсегда</button>{' '}
                            <button className="btn btn-success mt-3" ref={historyBack}>Нет, вернуться назад</button>
                        </div>
                    </>
                );
                break;

            case ActionType.Refresh:
                second = (
                    <>
                        {pageHeader(<>Обновление поисковых циферок <b>{getSearchTag()}</b>: {getTitle()}</>)}
                        <div>
                            <p>
                                {' Эти аккорды приватные, не видны в библиотеках других людей, но если вы скажете кому-то циферки '}<b>{getSearchTag()}</b>{' и они введут их в поиске, то они увидят ваши аккорды. '}
                            </p>{' '}
                            <p>
                                {' Еще эти аккорды доступны в интернете по ссылке, ею можно поделиться - '}<a href={f.collective!.uri.toString()}>{options.localSourceNameSafe}</a>{'. Собственно, чтобы отобрать доступ, если вы кому-то дали циферки или ссылку - обновите циферки. '}
                            </p>
                        </div>
                        <div className="mt-1 mb-1">
                            <button className="btn btn-warning me-1 mt-3" onClick={on(refresh)}>Да, обновить циферки</button>{' '}
                            <button className="btn btn-success mt-3" ref={historyBack}>Нет, вернуться назад</button>
                        </div>
                    </>
                );
                break;

            case ActionType.Add:
            case ActionType.Update: {
                const error = getAddUpdateError();
                second = (
                    <>
                        {pageHeader(currentAction === ActionType.Add ? parameters.versionId !== null ? `Новые аккорды к песне: ${getTitle()}` : 'Новые аккорды' : `Редактирование: ${getTitle()}`)}
                        <div className="mt-1 mb-1">
                            <button className={`btn me-1 mt-3 ${error === null ? 'btn-success' : 'btn-light disabled'}`} onClick={on(create)}>{error ?? (currentAction === ActionType.Add ? 'Создать!' : 'Сохранить!')}</button>{' '}
                            <button className="btn btn-light mt-3" ref={historyBack}>Нет, вернуться назад</button>
                        </div>
                        <div className=" mb-1 mt-3">
                            {' Имя: '}
                            <input type="text" maxLength={100} className="form-control" placeholder="Дайте нам имя..." ref={blazorValue(f.newName ?? getSongName())} onInput={on((x: FormEvent<HTMLInputElement>) => { f.newName = x.currentTarget.value; })} />
                        </div>
                        <div className=" mb-1 mt-3">
                            {' Автор '}<span className="opacity-75">(несколько — через запятую):</span>{' '}
                            <input type="text" maxLength={100} className="form-control" placeholder="Чьи мы?..." ref={blazorValue(f.newAuthor ?? getAuthors())} onInput={on((x: FormEvent<HTMLInputElement>) => { f.newAuthor = x.currentTarget.value; })} />
                        </div>
                        <div className=" mb-3 mt-3">
                            {' Видимость: '}
                            <label className="form-check mt-2">
                                <input type="radio" className="form-check-input" name="r-0" value="Private" ref={combineRefs<HTMLInputElement>(blazorChecked(f.collective?.collective.visibility === CollectiveVisibility.Private), nativeChange<HTMLInputElement>(on(() => { f.newAccess = CollectiveVisibility.Private; })))} />{' '}
                                <span className="form-check-label"><span className="my-bolder">Приватные.</span> Видны только вам, либо тем, кому вы сами сказали шесть цифр или дали ссылку :)</span>
                            </label>{' '}
                            <label className="form-check mt-2">
                                <input type="radio" className="form-check-input" name="r-0" value="Club" ref={combineRefs<HTMLInputElement>(blazorChecked(f.collective?.collective.visibility == null || f.collective.collective.visibility === CollectiveVisibility.Club), nativeChange<HTMLInputElement>(on(() => { f.newAccess = CollectiveVisibility.Club; })))} />{' '}
                                <span className="form-check-label"><span className="my-bolder">Внутри клуба.</span> Видны всем в нашем клубе в Шкафу, и еще их можно найти в телеграм-боте поиском, можно генерить картинки, и падают в ветку Библиотека в телеге.</span>
                            </label>{' '}
                            <label className="form-check mt-2">
                                <input type="radio" className="form-check-input" name="r-0" value="Public" ref={combineRefs<HTMLInputElement>(blazorChecked(f.collective?.collective.visibility === CollectiveVisibility.Public), nativeChange<HTMLInputElement>(on(() => { f.newAccess = CollectiveVisibility.Public; })))} />{' '}
                                <span className="form-check-label"><span className="my-bolder">На весь интернет.</span> Пока что не публикуется, но когда-нибудь — да.</span>
                            </label>
                        </div>
                        <div className="row mt-0 pt-0" id="my-row">
                            <div className="col-12 col-lg-6" id="my-col1">
                                <div className="page-header bg-success bg-opacity-75 my-header mt-3">
                                    <div className="page-header-content d-block">
                                        <div className="d-flex justify-content-between">
                                            <div className="breadcrumb">
                                                <h5 className="my-pt-25">Пишем тут:</h5>
                                            </div>
                                        </div>
                                    </div>
                                </div>{' '}
                                <textarea className="mt-0" id="my-editor" ref={textareaText(f.collective?.collective.contents ?? f.content)} onInput={on((x: FormEvent<HTMLTextAreaElement>) => contentUpdated(x.currentTarget.value))}></textarea>
                            </div>{' '}
                            <div className="col-12 col-lg-6" id="my-col1">
                                <div className="page-header bg-info bg-opacity-75 my-header mt-3">
                                    <div className="page-header-content d-block">
                                        <div className="d-flex justify-content-between">
                                            <div className="breadcrumb">
                                                <h5 className="my-pt-25">
                                                    {f.refreshing && <i className="ph-arrows-clockwise my-rotating me-2"></i>}
                                                    {' Получится так: '}
                                                </h5>
                                            </div>
                                        </div>
                                    </div>
                                </div>{' '}
                                <div id="my-preview">
                                    <div className="chords-block" dangerouslySetInnerHTML={{ __html: f.preview }}>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </>
                );
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        return (
            <>
                <div>
                    <ul>
                        {first}
                        <li>Вы всегда можете изменить видимость аккордов, которые вы добавили — скрыть или показать, или удалить их.</li>
                        {editing}
                        <li>Дизайн будет улучшаться, пока какой есть, простите. :)</li>{' '}
                        <li>Песни с вашими аккордами <b>— <a href={collectionNavigation.getExternalMyCollectivesNavigationLink()}>по фильтру в библиотеке</a></b>.</li>
                    </ul>
                </div>
                {second}
            </>
        );
    }

    return (
        <>
            <PageTitle>{getPageTitle()}</PageTitle>
            {renderBody()}
        </>
    );
}

/** `x!.Value` on a Guid? */
function valueOfGuid(value: Guid | null): Guid {
    if (value === null) throw new InvalidOperationException('Nullable object must have a value.');
    return value;
}

/** A member access on a null reference. */
function nonNull<T>(value: T | null): T {
    if (value === null) throw new TypeError('Object reference not set to an instance of an object.');
    return value;
}
