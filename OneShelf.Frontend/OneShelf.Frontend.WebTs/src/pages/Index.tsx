// The original: Pages/Index.razor (with Pages/Index.razor.js, unchanged in public/).
//
// OnInitializedAsync reads the release version through Index.razor.js, then, without an identity, goes to /login;
// the page renders before that completes and again after. After the first render: CollectionIndexProvider.MightSync,
// IllustrationsProvider.MaybeInit, ChordsCacheLoader.StartDelayed.

import { useEffect, useRef, type FormEvent } from 'react';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { navigationManager } from '../framework/navigation';
import { PageTitle } from '../framework/PageTitle';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { importJsModule, invokeJs, type IndexRazorJs } from '../interop/jsRuntime';
import { isNullOrWhiteSpace } from '../lib/common/dotnet/text';
import { services } from '../services/registry';
import { Tuner as TunerPage } from './Tuner';
import './Index.scoped.scss';
import { errorHandledTask } from '../framework/unhandledExceptions';

export function Index() {
    const identityProvider = services.identityProvider;
    const collectionNavigation = services.collectionNavigation;
    const collectionIndexProvider = services.collectionIndexProvider;
    const illustrationsProvider = services.illustrationsProvider;
    const chordsCacheLoader = services.chordsCacheLoader;
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({ releaseVersion: null as string | null }).current;

    useEffect(() => {
        // OnInitializedAsync
        void errorHandledTask((async () => {
            const jsModule = await importJsModule<IndexRazorJs>('./Pages/Index.razor.js');
            fields.releaseVersion = await invokeJs<string>(jsModule.getWasmReleaseVersion);

            if (identityProvider.identity === null) {
                navigationManager.navigateTo('/login');
            }

            stateHasChanged();
        })());

        // OnAfterRenderAsync(firstRender)
        collectionIndexProvider.mightSync();
        illustrationsProvider.maybeInit();
        chordsCacheLoader.startDelayed();
    }, [fields, identityProvider, collectionIndexProvider, illustrationsProvider, chordsCacheLoader, stateHasChanged]);

    const change = (value: string): void => {
        if (!isNullOrWhiteSpace(value)) {
            navigationManager.navigateTo(collectionNavigation.getExternalNavigationLink(value));
        }
    };

    const tuner = (): void => {
        TunerPage.init();
        navigationManager.navigateTo('/tuner');
    };

    const on = <TArgs extends unknown[]>(handler: (...args: TArgs) => void) => blazorEventHandler(stateHasChanged, handler);

    return (
        <>
            <PageTitle>Один Шкаф</PageTitle>

            {identityProvider.identity !== null && (
                <>
                    <div className="my-hide-standalone d-block d-lg-none mb-3">Чтобы шкаф работал не в браузере, а как приложение офлайн, <a href="/how-to-install">установите его на телефон</a>.</div>

                    <div {...{ col: 'row' }}>
                        <div className="col-md-6">
                            <div className="input-group mt-2">
                                <div className="input-field">
                                    <input type="text" className="form-control" placeholder="Поиск..." onInput={on((e: FormEvent<HTMLInputElement>) => { change(e.currentTarget.value); })} />
                                </div>{' '}
                                <div className="info mt-1">Можно искать песню, исполнителя, номер, какие-то слова из песни...</div>
                            </div>
                        </div>
                    </div>

                    <div className="section">
                        <div className="majorinfo">
                            <p>Добрый день!</p>{' '}
                            <p>На странице <a href="/cache">сохраненные данные</a> можно тыкнуть и вся библиотека станет доступна офлайн.</p>{' '}
                            <p>Последние новости:</p>{' '}
                            <ul>
                                <li>Поиск аккордовых последовательностей <span className="ph-line-segments"></span>,</li>{' '}
                                <li>Упрощенные аккорды,</li>{' '}
                                <li>Иллюстрации сгенеренные собакой :).</li>
                            </ul>
                        </div>
                    </div>

                    <div className="section">
                        <button className="btn btn-outline-primary my-bolder" onClick={on(() => navigationManager.navigateTo('/collection'))}>
                            {' '}→&nbsp;{' '}
                            <span className="ph-playlist my-red"></span>{' '}
                            Библиотека ←{' '}
                        </button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo(collectionNavigation.getExternalNavigationLinkMyRatingNewest()))}>
                            <span className="fas fa-heart my-red"></span>{' '}
                            &nbsp;Мой шортлист{' '}
                        </button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/collective'))}>
                            <span className="ph-music-notes-plus"></span>{' '}
                            &nbsp;Добавить мои аккорды{' '}
                        </button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/illustrations'))}>
                            <span className="ph-sun"></span>{' '}
                            &nbsp;Иллюстрации{' '}
                        </button>{' '}
                        <button className="btn btn-outline-primary text-warning" onClick={on(tuner)}>
                            <span className="ph-gauge text-warning"></span>{' '}
                            &nbsp;Тюнер{' '}
                        </button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/cache'))}>
                            <span className="ph-cloud-arrow-down"></span>{' '}
                            &nbsp;Сохраненные данные{' '}
                        </button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/fingerings'))}>
                            <span className="ph-kanban"></span>{' '}
                            &nbsp;Аппликатуры на гитаре{' '}
                        </button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/categories'))}>Списочки</button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/visitedsearch'))}>Недавний поиск</button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/visitedchords'))}>Недавние аккорды</button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/user'))}>Профиль</button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/artists'))}>Исполнители</button>{' '}
                        <button className="btn btn-outline-primary" onClick={on(() => navigationManager.navigateTo('/releasenotes'))}>Что умеет этот шкаф?</button>
                    </div>

                    <div className="section">
                    </div>

                    <div className="section majorinfo">
                        <p>Спасибо за ваш интерес!</p>{' '}
                        <span>Номер версии: <b>{fields.releaseVersion}</b></span>{' '}
                        <p>Если что-то плохо работает или можно улучшить, пожалуйста, <a target="_blank" href="https://t.me/pavel_zhur">пишите мне</a> или <a target="_blank" href="https://github.com/pavel-zhur/HarmonyDB/issues">репортите баг</a>. Очень хочу знать и сделать удобно.</p>{' '}
                        <p>Если кто любит дизайн, css и ux и вы хотите помочь, <a href="https://t.me/pavel_zhur">пишите!</a></p>{' '}
                        <p>Открытый исходный код, <a target="_blank" href="https://github.com/pavel-zhur/HarmonyDB">https://github.com/pavel-zhur/HarmonyDB</a>.</p>
                    </div>
                </>
            )}
        </>
    );
}
