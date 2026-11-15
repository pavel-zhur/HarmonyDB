// The original: Shared/MenuBottom.razor.

import { CollectionNavLink } from '../components/CollectionNavLink';
import { NavLink } from '../components/NavLink';
import { NavLinkMatch } from '../components/navLinkMatching';
import { VisitedNavLink } from '../components/VisitedNavLink';
import { useRenderOnEvent } from '../hooks/useEvent';
import { services } from '../services/registry';

export function MenuBottom() {
    const identityProvider = services.identityProvider;

    // OnInitializedAsync: IdentityProvider.IdentityChange += StateHasChanged;
    useRenderOnEvent(identityProvider.identityChange);

    return (
        <div className="navbar-sm border-top bg-white my-footer">
            <div className="navbar container-fluid bg-primary bg-opacity-25 justify-content-between">
                <div className="container-fluid">
                    <button type="button" className="navbar-toggler sidebar-mobile-main-toggle rounded my-ms--1 d-lg-none">
                        <i className="ph-list"></i>
                    </button>
                    <div className="d-none d-lg-block"></div>
                    <ul className="nav">
                        {identityProvider.identity !== null
                            ? (
                                <>
                                    <li className="nav-item">
                                        <NavLink href="" match={NavLinkMatch.All} activeClass="text-primary bg-primary bg-opacity-10 fw-semibold" className="navbar-nav-link navbar-nav-link-icon rounded">
                                            <div className="d-flex align-items-center mx-md-1">
                                                <i className="ph-house"></i>{' '}
                                                <span className="d-none d-md-inline-block ms-2">Главная</span>
                                            </div>
                                        </NavLink>
                                    </li>
                                    <li className="nav-item ms-md-1">
                                        <CollectionNavLink href="collection" isMy={false} activeClass="text-primary bg-primary bg-opacity-10 fw-semibold" className="navbar-nav-link navbar-nav-link-icon rounded">
                                            <div className="d-flex align-items-center mx-md-1">
                                                <i className="ph-playlist my-red"></i>{' '}
                                                <span className="d-none d-md-inline-block ms-2">Библиотека</span>
                                            </div>
                                        </CollectionNavLink>
                                    </li>
                                    <li className="nav-item ms-md-1">
                                        <CollectionNavLink href="mycollection" isMy={true} activeClass="text-primary bg-primary bg-opacity-10 fw-semibold" className="navbar-nav-link navbar-nav-link-icon rounded">
                                            <div className="d-flex align-items-center mx-md-1">
                                                <i className="fas fa-heart my-red"></i>{' '}
                                                <span className="d-none d-md-inline-block ms-2">Мой шортлист</span>
                                            </div>
                                        </CollectionNavLink>
                                    </li>
                                    <li className="nav-item ms-md-1">
                                        <VisitedNavLink href="visitedsearch" activeClass="text-primary bg-primary bg-opacity-10 fw-semibold" className="navbar-nav-link navbar-nav-link-icon rounded">
                                            <div className="d-flex align-items-center mx-md-1">
                                                <i className="ph-clock-counter-clockwise"></i>{' '}
                                                <span className="d-none d-md-inline-block ms-2">Недавнее</span>
                                            </div>
                                        </VisitedNavLink>
                                    </li>
                                </>
                            )
                            : (
                                <li className="nav-item">
                                    <NavLink href="login" activeClass="text-primary bg-primary bg-opacity-10 fw-semibold" className="navbar-nav-link navbar-nav-link-icon rounded">
                                        <div className="d-flex align-items-center mx-md-1">
                                            <i className="ph-house"></i>{' '}
                                            <span className="d-none d-md-inline-block ms-2">Вход</span>
                                        </div>
                                    </NavLink>
                                </li>
                            )}
                    </ul>
                    <div></div>
                </div>
            </div>
        </div>
    );
}
