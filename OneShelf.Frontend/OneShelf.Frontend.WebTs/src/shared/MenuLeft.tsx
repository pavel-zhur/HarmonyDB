// The original: Shared/MenuLeft.razor (with Shared/MenuLeft.razor.js, unchanged in public/).

import { CollectionNavLink } from '../components/CollectionNavLink';
import { NavLink } from '../components/NavLink';
import { NavLinkMatch } from '../components/navLinkMatching';
import { hostEnvironment } from '../configuration';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { delay } from '../dotnet/tasks';
import { useRenderOnEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { importJsModule, invokeJs, type MenuLeftRazorJs } from '../interop/jsRuntime';
import { Tuner } from '../pages/Tuner';
import { services } from '../services/registry';

export function MenuLeft() {
    void services.collectionNavigation; // @inject CollectionNavigation (unused)
    const identityProvider = services.identityProvider;
    const stateHasChanged = useStateHasChanged();

    // OnInitialized: IdentityProvider.IdentityChange += IdentityProviderOnIdentityChange (StateHasChanged)
    useRenderOnEvent(identityProvider.identityChange);

    const tunerClick = async (): Promise<void> => {
        await itemClick();
        await delay(500);
        Tuner.init();
    };

    const itemClick = async (): Promise<void> => {
        const module = await importJsModule<MenuLeftRazorJs>('./Shared/MenuLeft.razor.js');
        await invokeJs<void>(module.Clicked);
    };

    const onItemClick = blazorEventHandler(stateHasChanged, itemClick);
    const onTunerClick = blazorEventHandler(stateHasChanged, tunerClick);

    return (
        <div className="sidebar sidebar-dark sidebar-main sidebar-expand-lg">

            <div className="sidebar-content">

                <div className="sidebar-section">
                    <div className="sidebar-section-body d-flex justify-content-center">
                        <h5 className="sidebar-resize-hide flex-grow-1 my-auto">Навигация</h5>

                        <div>
                            <button type="button" className="btn btn-flat-white btn-icon btn-sm rounded-pill border-transparent sidebar-control sidebar-main-resize d-none d-lg-inline-flex">
                                <i className="ph-arrows-left-right"></i>
                            </button>{' '}

                            <button type="button" className="btn btn-flat-white btn-icon btn-sm rounded-pill border-transparent sidebar-mobile-main-toggle d-lg-none" id="mobileclosebutton">
                                <i className="ph-x"></i>
                            </button>
                        </div>
                    </div>
                </div>
                <div className="sidebar-section">
                    <ul className="nav nav-sidebar" data-nav-type="accordion">

                        <li className="nav-item-header pt-0">
                            <div className="text-uppercase fs-sm lh-sm opacity-50 sidebar-resize-hide">Главное</div>{' '}
                            <i className="ph-dots-three sidebar-resize-show"></i>
                        </li>

                        {identityProvider.identity === null
                            ? (
                                <li className="nav-item">
                                    <NavLink onClick={onItemClick} href="login" className="nav-link">
                                        <i className="ph-sign-in"></i>{' '}
                                        <span>Вход</span>
                                    </NavLink>
                                </li>
                            )
                            : (
                                <>
                                    <li className="nav-item">
                                        <NavLink onClick={onItemClick} href="" className="nav-link" match={NavLinkMatch.All}>
                                            <i className="ph-house"></i>{' '}
                                            <span>Главная</span>
                                        </NavLink>
                                    </li>
                                    {hostEnvironment.environment.startsWith('Development') && (
                                        <>
                                            <li className="nav-item">
                                                <NavLink onClick={onItemClick} href="/index2" className="nav-link">
                                                    <i className="ph-house"></i>{' '}
                                                    <span>Главная-2</span>
                                                </NavLink>
                                            </li>
                                            <li className="nav-item">
                                                <NavLink href="/progressions" className="nav-link">
                                                    <i className="ph-line-segments"></i>{' '}
                                                    <span>Прогрессии аккордов</span>
                                                </NavLink>
                                            </li>
                                        </>
                                    )}
                                    <li className="nav-item">
                                        <NavLink onClick={onItemClick} href="/user" className="nav-link">
                                            <i className="ph-user"></i>{' '}
                                            <span>Профиль</span>
                                        </NavLink>
                                    </li>
                                    <li className="nav-item">
                                        <NavLink onClick={onItemClick} href="/cache" className="nav-link">
                                            <i className="ph-cloud-arrow-down"></i>{' '}
                                            <span>Сохранённые данные</span>
                                        </NavLink>
                                    </li>
                                    <li className="nav-item">
                                        <NavLink onClick={onItemClick} href="/categories" className="nav-link">
                                            <i className="ph-list-dashes"></i>{' '}
                                            <span>Списочки</span>
                                        </NavLink>
                                    </li>
                                </>
                            )}

                        <li className="nav-item">
                            <NavLink onClick={onItemClick} href="/illustrations" className="nav-link">
                                <i className="ph-sun"></i>{' '}
                                <span>Иллюстрации</span>
                            </NavLink>
                        </li>

                        {identityProvider.identity !== null && (
                            <>
                                <li className="nav-item-header">
                                    <div className="text-uppercase fs-sm lh-sm opacity-50 sidebar-resize-hide">Библиотека</div>{' '}
                                    <i className="ph-dots-three sidebar-resize-show"></i>
                                </li>
                                <li className="nav-item">
                                    <CollectionNavLink onClick={onItemClick} href="collection" className="nav-link" isMy={false}>
                                        <i className="ph-playlist my-red"></i>{' '}
                                        <span>Все песни</span>
                                    </CollectionNavLink>
                                </li>
                                <li className="nav-item">
                                    <CollectionNavLink onClick={onItemClick} href="mycollection" className="nav-link" isMy={true}>
                                        <i className="fas fa-heart my-red"></i>{' '}
                                        <span>Мой шортлист</span>
                                    </CollectionNavLink>
                                </li>
                                <li className="nav-item">
                                    <NavLink onClick={onItemClick} href="/artists" className="nav-link">
                                        <i className="ph-user-list"></i>{' '}
                                        <span>Исполнители</span>
                                    </NavLink>
                                </li>
                                <li className="nav-item">
                                    <NavLink onClick={onItemClick} href="/collective" className="nav-link">
                                        <i className="ph-music-notes-plus"></i>{' '}
                                        <span>Добавить мои аккорды</span>
                                    </NavLink>
                                </li>
                                <li className="nav-item-header">
                                    <div className="text-uppercase fs-sm lh-sm opacity-50 sidebar-resize-hide">Недавнее</div>{' '}
                                    <i className="ph-dots-three sidebar-resize-show"></i>
                                </li>
                                <li className="nav-item">
                                    <NavLink onClick={onItemClick} href="/visitedsearch" className="nav-link">
                                        <i className="ph-magnifying-glass"></i>{' '}
                                        <span>Поиск</span>
                                    </NavLink>
                                </li>
                                <li className="nav-item">
                                    <NavLink onClick={onItemClick} href="/visitedchords" className="nav-link">
                                        <i className="ph-music-notes-simple"></i>{' '}
                                        <span>Аккорды</span>
                                    </NavLink>
                                </li>
                                <li className="nav-item-header">
                                    <div className="text-uppercase fs-sm lh-sm opacity-50 sidebar-resize-hide">Инструменты</div>{' '}
                                    <i className="ph-dots-three sidebar-resize-show"></i>
                                </li>
                                <li className="nav-item">
                                    <NavLink onClick={onTunerClick} href="/tuner" className="nav-link">
                                        <i className="ph-gauge"></i>{' '}
                                        <span>Тюнер</span>
                                    </NavLink>
                                </li>
                                <li className="nav-item">
                                    <NavLink onClick={onItemClick} href="/fingerings" className="nav-link">
                                        <i className="ph-kanban"></i>{' '}
                                        <span>Аппликатуры на гитаре</span>
                                    </NavLink>
                                </li>
                                <li className="nav-item-header">
                                    <div className="text-uppercase fs-sm lh-sm opacity-50 sidebar-resize-hide">Инфо</div>{' '}
                                    <i className="ph-dots-three sidebar-resize-show"></i>
                                </li>
                                <li className="nav-item">
                                    <NavLink onClick={onItemClick} href="/releasenotes" className="nav-link">
                                        <i className="ph-robot"></i>{' '}
                                        <span>Что умеет этот шкаф?</span>
                                    </NavLink>
                                </li>
                            </>
                        )}

                        <li className="nav-item">
                            <NavLink onClick={onItemClick} href="/how-to-install" className="nav-link">
                                <i className="ph-cloud-arrow-down"></i>{' '}
                                <span>Как установить?</span>
                            </NavLink>
                        </li>

                    </ul>
                </div>

            </div>

        </div>
    );
}
