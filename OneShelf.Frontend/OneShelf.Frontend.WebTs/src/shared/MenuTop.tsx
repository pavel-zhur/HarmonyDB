// The original: Shared/MenuTop.razor.

import { useRenderOnEvent } from '../hooks/useEvent';
import { isNullOrWhiteSpace } from '../lib/common/dotnet/text';
import { services } from '../services/registry';
import { ChordsCacheLoaderProgress } from './ChordsCacheLoaderProgress';
import { FingeringIndicator } from './FingeringIndicator';
import { SyncingIndicator } from './SyncingIndicator';
import { UpdateAvailableDetector } from './UpdateAvailableDetector';
import './MenuTop.scoped.scss';
import { stringAttributes } from '../framework/attributes';

export function MenuTop() {
    const identityProvider = services.identityProvider;
    const logoImageSequencer = services.logoImageSequencer;

    // OnInitializedAsync: LogoImageSequencer.Change += StateHasChanged; IdentityProvider.IdentityChange += StateHasChanged;
    useRenderOnEvent(logoImageSequencer.change);
    useRenderOnEvent(identityProvider.identityChange);

    return (
        <>
            <div className="my-navbar-black-image"></div>
            <div className="container-fluid">
                <div className="d-flex d-lg-none me-2 my-ms--1">
                    <button type="button" className="navbar-toggler sidebar-mobile-main-toggle rounded">
                        <i className="ph-list"></i>
                    </button>
                </div>

                <div className="navbar-brand">
                    <a href="/" className="d-inline-flex align-items-center my-logo">
                        <div className={logoImageSequencer.current}></div>
                        <h5 className="ms-2" {...{ alt: '' }} id="my-top-title">
                            <span>Один Шкаф</span>{' '}
                            <ChordsCacheLoaderProgress />
                        </h5>
                    </a>
                </div>

                <ul className="nav gap-sm-2 order-1 order-lg-2 ms-auto">

                    <UpdateAvailableDetector />

                    <SyncingIndicator />

                    <FingeringIndicator />

                    <li className="nav-item">
                        <a className="navbar-nav-link align-items-center rounded p-1" href="/user">
                            <div className="h-32px my-userpic">
                                {isNullOrWhiteSpace(identityProvider.identity?.photoUrl)
                                    ? <i className="ph-user pt-1"></i>
                                    : (
                                        <>
                                            <img ref={stringAttributes({ style: `background-image: url('${identityProvider.identity!.photoUrl!}')` })} src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7" className="w-32px h-32px rounded" alt="" />
                                            <div><i className="ph-user pt-1"></i></div>
                                        </>
                                    )}
                            </div>

                            {!isNullOrWhiteSpace(identityProvider.identity?.title) && (
                                <span className="d-none d-lg-inline-block mx-lg-2">{identityProvider.identity!.title}</span>
                            )}
                        </a>
                    </li>
                </ul>
            </div>
        </>
    );
}
