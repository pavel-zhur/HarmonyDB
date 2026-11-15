// The original: Pages/VisitedSearch.razor.
//
// OnInitializedAsync checks the authorization; OnParametersSetAsync (each time the query string changes) loads the
// page of the history, keeping the previous page on the screen until the new one arrives.
import { useLayoutEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { getLogger } from '../framework/logging';
import { getUriWithQueryParameter } from '../framework/navigationManagerExtensions';
import { useQueryParameters } from '../framework/supplyParameterFromQuery';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { errorHandledTask } from '../framework/unhandledExceptions';
import type { GetVisitedSearchesResponse } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/GetVisitedSearchesResponse';
import { toLongDateString, toShortTimeString } from '../lib/common/dotnet/DateTimeFormat';
import { services } from '../services/registry';
import './VisitedSearch.scoped.scss';

const logger = getLogger('OneShelf.Frontend.Web.Pages.VisitedSearch');

export function VisitedSearch() {
    const api = services.api;
    const identityProvider = services.identityProvider;
    const collectionNavigation = services.collectionNavigation;
    const stateHasChanged = useStateHasChanged();

    const query = useQueryParameters();
    const page = query.int('Page');

    const self = useRef({ visits: null as GetVisitedSearchesResponse | null, failed: false, page }).current;
    self.page = page;

    const tryLoad = async () => {
        try {
            self.visits = await api.getVisitedSearches(self.page);
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
                    <h3>Недавний поиск</h3>
                </div>
            </div>
            <div className="align-content-sm-center">
                <div className="row ">
                    <div className="col-md-auto">
                        <div className="row ">
                            <div className="input-group artists-switch">
                                <div className="input-group-prepend ">
                                    <a className="btn btn-secondary px-2">Недавний поиск</a>
                                </div>
                                <div className="input-group-prepend ">
                                    <a className="btn btn-secondary  btn-secondary-active px-2" href="/visitedchords">Недавние аккорды</a>
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
            ) : visits === null ? (
                <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
            ) : (
                <>
                    <table className="mt-3 songs table  table-artistcompact ">
                        <thead>
                            <tr>
                                <td><span>Что</span></td>
                                <td><span className="px-2">Когда</span></td>
                            </tr>
                        </thead>
                        {visits.visitedSearches.map((visit, i) => (
                            <tr key={i}>
                                <td><a href={collectionNavigation.getExternalNavigationLink(visit.query)}>{visit.query}</a></td>
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
                </>
            )}
        </div>
    );
}
