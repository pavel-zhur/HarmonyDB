// The original: Pages/Cache.razor (@inherits CollectionComponentBase).
//
// As there, the loaders' Updated handlers are added only once the first Recalculate has completed (also when the page
// has gone by then, and then they stay), and removed when the page goes.

import { useEffect, useRef } from 'react';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { CacheLoaderBase } from '../services/CacheLoaderBase';
import { services } from '../services/registry';
import { stringAttributes } from '../framework/attributes';
import { errorHandledTask } from '../framework/unhandledExceptions';

interface Counters {
    songs: number;
    chords: number;
    chordsCached: number;
    additionalChordsCached: number;
    progressionsBuilt: number;
}

export function Cache() {
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({ counters: null as Counters | null }).current;

    const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(async () => {
        // OnNewCollectionIndexReceived
        await recalculate();
        stateHasChanged();
    });

    const identityProvider = services.identityProvider;
    const dataProvider = services.dataProvider;
    const instantActions = services.instantActions;
    const collectionIndexProvider = services.collectionIndexProvider;
    const chordsCacheLoader = services.chordsCacheLoader;
    const illustrationsProvider = services.illustrationsProvider;
    const progressionsCacheLoader = services.progressionsCacheLoader;
    const jsFunctions = services.jsFunctions;

    /** CollectionIndex, as the base class reads it: the provider's at the moment of reading. */
    const getCollectionIndex = () => collectionIndexProvider.peek().collectionIndex;

    /** The library's versions' external ids (SelectMany over SongsById's versions with an ExternalId). */
    const collectionExternalIds = (): string[] => {
        const collection = getCollectionIndex()!;
        return [...collection.songsById.values()].flatMap(x => x.versions.filter(v => v.externalId !== null).map(v => v.externalId!));
    };

    const recalculate = async (): Promise<void> => {
        if (getCollectionIndex() === null) return;

        const availableChords = await dataProvider.getChordsExternalIdsAvailableInCache();
        const availableProgressions = await dataProvider.getProgressionsExternalIdsAvailableInCache();

        const collection = getCollectionIndex()!;
        const externalIds = collectionExternalIds();

        const chordsCached = externalIds.filter(x => availableChords.has(x)).length;
        const progressionsBuilt = externalIds.filter(x => availableProgressions.has(x)).length;

        fields.counters = {
            songs: collection.songsById.size,
            chords: [...collection.songsById.values()].reduce((sum, x) => sum + x.versions.filter(v => v.source !== null).length, 0),
            chordsCached,
            additionalChordsCached: availableChords.size - chordsCached,
            progressionsBuilt,
        };

        stateHasChanged();
    };

    const recalculateChords = async (availableExternalIds: ReadonlySet<string> | null): Promise<void> => {
        if (getCollectionIndex() === null) return;

        availableExternalIds ??= await dataProvider.getChordsExternalIdsAvailableInCache();

        const chordsCached = collectionExternalIds().filter(x => availableExternalIds.has(x)).length;

        fields.counters = fields.counters === null ? null : {
            ...fields.counters,
            chordsCached,
            additionalChordsCached: availableExternalIds.size - chordsCached,
        };

        stateHasChanged();
    };

    const recalculateProgressions = async (availableExternalIds: ReadonlySet<string> | null): Promise<void> => {
        if (getCollectionIndex() === null) return;

        availableExternalIds ??= await dataProvider.getProgressionsExternalIdsAvailableInCache();

        const progressionsBuilt = collectionExternalIds().filter(x => availableExternalIds.has(x)).length;

        fields.counters = fields.counters === null ? null : {
            ...fields.counters,
            progressionsBuilt,
        };

        stateHasChanged();
    };

    // The handlers as added: the latest render's functions.
    const handlers = useRef({ recalculate, recalculateChords, recalculateProgressions });
    handlers.current.recalculate = recalculate;
    handlers.current.recalculateChords = recalculateChords;
    handlers.current.recalculateProgressions = recalculateProgressions;

    // OnInitializedAsync (after the base's); Dispose
    useEffect(() => {
        const onChordsUpdated = (ids: ReadonlySet<string> | null) => handlers.current.recalculateChords(ids);
        const onProgressionsUpdated = (ids: ReadonlySet<string> | null) => handlers.current.recalculateProgressions(ids);

        void errorHandledTask((async () => {
            identityProvider.ensureAuthorized();

            await handlers.current.recalculate();

            chordsCacheLoader.updated.add(onChordsUpdated);
            progressionsCacheLoader.updated.add(onProgressionsUpdated);
        })());

        return () => {
            chordsCacheLoader.updated.remove(onChordsUpdated);
            progressionsCacheLoader.updated.remove(onProgressionsUpdated);
        };
    }, [identityProvider, chordsCacheLoader, progressionsCacheLoader]);

    const reload = async (): Promise<void> => {
        await dataProvider.clear();
        await instantActions.clear();
        await collectionIndexProvider.clear();
        await illustrationsProvider.clear();
    };

    const reloadProgressions = async (): Promise<void> => {
        await dataProvider.clearProgressions();
    };

    const startBuildingProgressions = async (): Promise<void> => {
        if (fields.counters === null) return;

        if (fields.counters.chords - fields.counters.chordsCached > CacheLoaderBase.MissingChordsThresholdForProgressionsSlowFail) {
            if (await jsFunctions.confirm('Сначала необходимо скачать все аккорды. Начать?')) {
                chordsCacheLoader.start();
            }

            return;
        }

        progressionsCacheLoader.start();
    };

    const on = (handler: () => void | Promise<void>) => blazorEventHandler(stateHasChanged, handler);

    const counters = fields.counters;

    return (
        <>
            <h1>Аккорды офлайн и прогрессии</h1>

            <div ref={stringAttributes({ style: 'margin-bottom: 20px' })}>
                {' '}На этой странице можно управлять двумя штуками:{' '}
            </div>

            <ul ref={stringAttributes({ style: 'margin-bottom: 20px' })}>
                <li>1. Вы можете скачать все аккорды из библиотеки клуба офлайн к себе на телефон, они будут открываться без интернета.</li>
                <li>2. Вы можете запустить анализ аккордов и построение последовательностей, если вас интересует поиск по ним.</li>
            </ul>

            <div ref={stringAttributes({ style: 'margin-bottom: 20px' })}>
                {' '}Жмите нужную кнопку и пользуйтесь приложением (с этой страницы можно уйти, но лучше остаться в приложении), скачивание (1) или построение (2) будет в фоне, <i>пока вы пользуетесь приложением и телефон не спит.</i> Занимает минуты три-пять. Оно немножко подглючивает, так что иногда возвращайтесь, пожалуйста, сюда и глядите, всё ли хорошо, и жмите кнопку снова если оно что-то остановилось, не дойдя до конца.{' '}
            </div>

            {collectionIndexFailed
                ? (
                    <>
                        <span>Не получилось загрузить, может нету интернета, библиотеку.</span>{' '}
                        <button className="btn btn-outline-secondary" onClick={on(retryCollectionIndex)}>Повтор</button>
                    </>
                )
                : collectionIndex === null || counters === null
                    ? <span><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></span>
                    : (
                        <>
                            <div>Песен в библиотеке клуба: <b>{counters.songs}</b></div>
                            <div>Аккордов в библиотеке клуба: <b>{counters.chords}</b></div>
                            <div>
                                {' '}Аккордов офлайн у вас: <b>{counters.chordsCached}</b>{' '}
                                <br />{' '}
                                <small className="text-warning">(eсли их чуть-чуть меньше, это нормально).</small>
                            </div>
                            <div>Дополнительно аккордов не из библиотеки офлайн у вас: <b>{counters.additionalChordsCached}</b></div>
                            <div>
                                {' '}Построено аккордовых прогрессий для анализа: <b>{counters.progressionsBuilt}</b>{' '}
                                <br />{' '}
                                <small className="text-warning">(eсли их чуть-чуть меньше, это нормально).</small>
                            </div>

                            {chordsCacheLoader.isRunning
                                ? (
                                    <>
                                        <div className="text-success mt-3">
                                            <b>Скачиваем (приложение может быть немного медленное пока идет скачивание)...</b>
                                        </div>
                                        <button className="btn btn-primary mt-2" onClick={on(chordsCacheLoader.stop)}>Стоп</button>
                                    </>
                                )
                                : progressionsCacheLoader.isRunning
                                    ? (
                                        <>
                                            <div className="text-success mt-3">
                                                <b>Строим прогрессии (приложение может быть немного медленное пока они строятся)...</b>
                                            </div>
                                            <button className="btn btn-primary mt-2" onClick={on(progressionsCacheLoader.stop)}>Стоп</button>
                                        </>
                                    )
                                    : (
                                        <div className="mt-2">
                                            <button className="btn btn-primary mt-2" onClick={on(chordsCacheLoader.start)}>1. Скачать все аккорды библиотеки офлайн</button>{' '}
                                            <button className="btn btn-primary mt-2" onClick={on(startBuildingProgressions)}>2. Построить аккордовые прогрессии для поиска</button>{' '}
                                            <button className="btn btn-outline-danger mt-2" onClick={on(reload)}>Почистить сохраненные данные если что-то идёт не так</button>{' '}
                                            <button className="btn btn-outline-danger mt-2" onClick={on(reloadProgressions)}>Почистить прогрессии</button>
                                        </div>
                                    )}
                        </>
                    )}
        </>
    );
}
