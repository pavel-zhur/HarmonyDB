// The original: Pages/Illustrations.razor.
//
// OnInitializedAsync loads the illustrations (anonymously without an identity or a collection index); "Повтор" calls
// it again. The page renders before the load completes and after.

import { Fragment, useEffect, useRef } from 'react';
import { getRequired } from '../dotnet/collections';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { inlineEventAttribute } from '../framework/inlineEventAttribute';
import { getLogger } from '../framework/logging';
import { navigationManager } from '../framework/navigation';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { IllustrationsResponse } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/IllustrationsResponse';
import { orderBy } from '../lib/common/dotnet/Linq';
import { services } from '../services/registry';
import './Illustrations.scoped.scss';
import { errorHandledTask } from '../framework/unhandledExceptions';

const logger = getLogger('OneShelf.Frontend.Web.Pages.Illustrations');

export function Illustrations() {
    const identityProvider = services.identityProvider;
    const api = services.api;
    const collectionNavigation = services.collectionNavigation;
    const jsFunctions = services.jsFunctions;
    const collectionIndexProvider = services.collectionIndexProvider;
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({
        result: null as IllustrationsResponse | null,
        failed: false,
    }).current;

    const onInitializedAsync = async (): Promise<void> => {
        fields.failed = false;
        try {
            const identity = identityProvider.identity;
            const collectionIndex = collectionIndexProvider.peek().collectionIndex;

            if (identity === null || collectionIndex === null) {
                fields.result = await api.getIllustrationsAnonymous();
            } else {
                fields.result = await api.getIllustrations(null, false);
            }
        } catch (e) {
            logger.logError(e, 'Error loading the illustrations');
            fields.result = null;
        }

        if (fields.result === null) {
            fields.failed = true;
        }
    };

    // OnInitializedAsync, then a render
    useEffect(() => {
        void errorHandledTask((async () => {
            await onInitializedAsync();
            stateHasChanged();
        })());
    }, []);

    const goTo = async (songId: number): Promise<void> => {
        if (identityProvider.identity === null) return;
        if (collectionIndexProvider.peek().collectionIndex?.songsById.has(songId) !== true) return;
        await jsFunctions.scrollToTop();
        navigationManager.navigateTo(collectionNavigation.getExternalIllustrationsNavigationLink(songId));
    };

    const result = fields.result;

    if (result === null) {
        return fields.failed
            ? (
                <>
                    <p><em>Не получилось загрузить, может нету интернета, библиотеку.</em></p>
                    <button className="btn btn-outline-secondary" onClick={blazorEventHandler(stateHasChanged, onInitializedAsync)}>Повтор</button>
                </>
            )
            : (
                <>
                    <span>Эта страничка грузится где-то секунд 20 обычно.</span>{' '}
                    <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
                </>
            );
    }

    // OrderByDescending(x => x.Value.LatestCreatedOn): stable
    const songs = orderBy(result.illustrations!.songs, ([, a], [, b]) => b.latestCreatedOn.compareTo(a.latestCreatedOn));

    return (
        <>
            <div className="page-header bg-warning bg-opacity-75 my-header my-content-addition">

                <div className="page-header-content d-block">
                    <div className="d-flex justify-content-between">
                        <div className="breadcrumb">
                            <h5 className="my-pt-25">Иллюстрации</h5>
                        </div>
                    </div>

                </div>
            </div>
            {identityProvider.identity !== null
                ? (
                    <div>
                        {' '}Привет!{' '}
                        <br />{' '}
                        Тут видны иллюстрации к песням, сгенеренные ботом. Они появляются тут спустя 96 часов.{' '}
                        <br />{' '}
                        Их же можно смотреть в <a href={collectionNavigation.getExternalIllustrationsNavigationLink()}>библиотеке с фильтром "С иллюстрациями"</a>, в самих песнях. Там, в песнях, их бывает больше, тут на каждую песню всего несколько.{' '}
                    </div>
                )
                : (
                    <div>
                        {' '}Привет!{' '}
                        <br />{' '}
                        Кому интересно, как их генерить, <a href="https://t.me/pavel_zhur">пишите</a>.{' '}
                    </div>
                )}

            {songs.map(([id, song]) => {
                const isLink = identityProvider.identity !== null && collectionIndexProvider.peek().collectionIndex?.songsById.has(id) === true;
                return (
                    <Fragment key={id}>
                        <div className={`page-header bg-info bg-opacity-75 mt-4 ${!isLink ? '' : 'my-link'} my-header my-content-addition`} onClick={blazorEventHandler(stateHasChanged, () => goTo(id))}>

                            <div className="page-header-content d-block">
                                <div className="d-flex justify-content-between">
                                    <div className="breadcrumb">
                                        <h5 className="my-pt-25">{getRequired(result.titles!, id)}</h5>
                                    </div>

                                    {isLink && (
                                        <div className="d-flex">
                                            <span className="d-flex align-items-center text-body py-2 ms-2 collapsed">
                                                <i className="ph-arrow-right my-white"></i>
                                            </span>
                                        </div>
                                    )}
                                </div>

                            </div>
                        </div>

                        <div className="d-flex my-images">
                            {song.illustrations.map((illustration, i) => (
                                <div key={i} className="img-box">
                                    <img src={illustration.publicUrls.url256.toString()} ref={inlineEventAttribute('onclick', ` illustrationFullscreenClicked(this, '${illustration.publicUrls.url1024.toString()}') `)} />{' '}
                                    <span>{`v${illustration.version} i${illustration.i + 1} j${illustration.j + 1} #${illustration.k + 1}`}</span>
                                </div>
                            ))}
                        </div>
                    </Fragment>
                );
            })}
        </>
    );
}
