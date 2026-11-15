// The original: Pages/VisitedChords.razor.
//
// OnInitializedAsync checks the authorization; OnParametersSetAsync (each time the query string changes) loads the
// page of the history, keeping the previous page on the screen until the new one arrives.
import { useLayoutEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { getLogger } from '../framework/logging';
import { getUriWithQueryParameter } from '../framework/navigationManagerExtensions';
import { useQueryParameters } from '../framework/supplyParameterFromQuery';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { GetVisitedChordsResponse } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/GetVisitedChordsResponse';
import type { VisitedChords as VisitedChordsItem } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/VisitedChords';
import { toLongDateString, toShortTimeString } from '../lib/common/dotnet/DateTimeFormat';
import { single } from '../lib/common/dotnet/Linq';
import { Uri } from '../lib/common/dotnet/Uri';
import type { CollectionIndex } from '../models/CollectionIndex';
import { services } from '../services/registry';
import './VisitedChords.scoped.scss';

const logger = getLogger('OneShelf.Frontend.Web.Pages.VisitedChords');

export function VisitedChords() {
    const api = services.api;
    const identityProvider = services.identityProvider;
    const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent();
    const collectionNavigation = services.collectionNavigation;
    const stateHasChanged = useStateHasChanged();

    const query = useQueryParameters();
    const page = query.int('Page');

    const self = useRef({ visits: null as GetVisitedChordsResponse | null, failed: false, page }).current;
    self.page = page;

    const tryLoad = async () => {
        try {
            self.visits = await api.getVisitedChords(self.page);
        } catch (e) {
            logger.logError(e, 'Error retrieving the history items.');
            self.visits = null;
            self.failed = true;
        }
    };

    const retryLoad = async () => {
        self.failed = false;
        await tryLoad();
    };

    // OnInitializedAsync
    useLayoutEffect(() => {
        identityProvider.ensureAuthorized();
    }, []);

    // OnParametersSetAsync
    const search = useLocation().search;
    useLayoutEffect(() => {
        void errorHandledTask(tryLoad().then(() => stateHasChanged()));
    }, [search]);

    const visits = self.visits;
    return (
        <div className="left-collection-col m-0">
            <div className="row">
                <div className="col-md-12 search-result-section-content">
                    <h3>Недавние аккорды</h3>
                </div>
            </div>
            <div className="align-content-sm-center ">
                <div className="row ">
                    <div className="col-md-auto">
                        <div className="row ">
                            <div className="input-group artists-switch">
                                <div className="input-group-prepend ">
                                    <a className="btn btn-secondary btn-secondary-active px-2" href="/visitedsearch">Недавний поиск</a>
                                </div>
                                <div className="input-group-prepend ">
                                    <b className="btn btn-secondary px-2">Недавние аккорды</b>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            {self.failed ? (
                <>
                    <span>Не получилось загрузить, может нету интернета, историю.</span>
                    <button className="btn btn-outline-secondary" onClick={blazorEventHandler(stateHasChanged, retryLoad)}>Повтор</button>
                </>
            ) : collectionIndexFailed ? (
                <>
                    <span>Не получилось загрузить, может нету интернета, библиотеку.</span>
                    <button className="btn btn-outline-secondary" onClick={blazorEventHandler(stateHasChanged, retryCollectionIndex)}>Повтор</button>
                </>
            ) : visits === null || collectionIndex === null ? (
                <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
            ) : (
                <>
                    <table className="mt-3 songs table  table-artistcompact ">
                        <thead>
                            <tr>
                                <td>#</td>
                                <td><span>Что</span></td>
                                <td className="text-center "><span className="px-2">Источник</span></td>
                                <td className="text-center"><span className="px-2">Трнсп</span></td>
                                <td className="text-center"><span className="px-2">Когда</span></td>
                            </tr>
                        </thead>
                        {visits.visitedChords.map((visit, i) => (
                            <tr key={i}>
                                <td><span className="px-2">{visit.songId !== null && collectionIndex.songsById.has(visit.songId) ? collectionIndex.songsById.get(visit.songId)!.index : null}</span></td>
                                <td>
                                    {renderWhat(visit, collectionIndex)}
                                </td>
                                <td><span className="px-2">{visit.source}</span></td>
                                <td><span className="px-2">{visit.transpose === 0 ? null : visit.transpose}</span></td>
                                <td><span className="px-2">{toLongDateString(visit.happenedOn.toLocalTime())} {toShortTimeString(visit.happenedOn.toLocalTime())}</span></td>
                            </tr>
                        ))}
                    </table>

                    <nav aria-label="Page navigation example ">
                        <ul className="pagination mt-5">
                            {page > 0 && (
                                <>
                                    {page >= 4 && (
                                        <li className="page-item page-previtem"><a className="page-link " tabIndex={-1} href={getUriWithQueryParameter('Page', 0)}> 1</a></li>
                                    )}
                                    <li className="page-item page-previtem"><a className="page-link " tabIndex={-1} href={getUriWithQueryParameter('Page', page - 1)}> Назад</a></li>
                                </>
                            )}
                            {Array.from({ length: Math.max(0, visits.pagesCount) }, (_, i) => i === page ? (
                                <li key={i} className="page-item active">
                                    <span className="page-link">
                                        <span className="sr-only ">{i + 1}</span>
                                    </span>
                                </li>
                            ) : page >= i - 3 && page <= i + 3 ? (
                                <li key={i} className="page-item">
                                    <a className="page-link " href={getUriWithQueryParameter('Page', i)}>{i + 1}</a>
                                </li>
                            ) : null)}
                            {page < visits.pagesCount - 1 && (
                                <>
                                    <li className="page-item page-nextitem"><a className="page-link " tabIndex={-1} href={getUriWithQueryParameter('Page', page + 1)}> Вперед</a></li>
                                    {page <= visits.pagesCount - 5 && (
                                        <li className="page-item page-nextitem"><a className="page-link " tabIndex={-1} href={getUriWithQueryParameter('Page', visits.pagesCount - 1)}>{visits.pagesCount}</a></li>
                                    )}
                                </>
                            )}
                        </ul>
                    </nav>
                    <div>
                    </div>
                </>
            )}
        </div>
    );

    function renderWhat(visit: VisitedChordsItem, index: CollectionIndex) {
        const song = visit.songId !== null && visit.uri !== null ? index.songsById.get(visit.songId) : undefined;
        if (song !== undefined && song.versions.some(x => Uri.equals(x.uri, visit.uri))) {
            const version = single(song.versions, x => Uri.equals(x.uri, visit.uri));
            return <a href={collectionNavigation.getExternalNavigationLink(visit.songId!, version.id, version.externalId)}>{visit.artists} - {visit.title}</a>;
        }

        if (visit.searchQuery !== null && visit.externalId !== null) {
            return <a href={collectionNavigation.getExternalNavigationLink(visit.searchQuery, visit.externalId, visit.artists, visit.title)}>{visit.artists} - {visit.title}</a>;
        }

        return <span>{visit.artists} - {visit.title}</span>;
    }
}
