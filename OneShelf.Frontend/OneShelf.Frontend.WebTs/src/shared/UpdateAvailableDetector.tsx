// The original: Shared/UpdateAvailableDetector.razor.
//
// When it appears it calls window.registerForUpdateAvailableNotification (sw-registrator.js, unchanged in public/)
// with a reference to itself; when a new service worker has installed while a page was controlled by one, the script
// calls OnUpdateAvailable, and the bell with "Есть обновление!" appears.

import { useEffect, useRef, useState } from 'react';
import { getLogger } from '../framework/logging';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { createDotNetObjectReference, invokeWindowFunction } from '../interop/jsRuntime';

const logger = getLogger('OneShelf.Frontend.Web.Shared.UpdateAvailableDetector');

/** The markup's inline `onclick="window.location.reload()"` (React does not set on* attributes itself). */
function setInlineOnClick(element: HTMLDivElement | null): void {
    element?.setAttribute('onclick', 'window.location.reload()');
}

export function UpdateAvailableDetector() {
    const [newVersionAvailable, setNewVersionAvailable] = useState(false);
    const initialized = useRef(false);

    // OnInitializedAsync
    useEffect(() => {
        if (initialized.current) return;
        initialized.current = true;

        void errorHandledTask((async () => {
            try {
                await invokeWindowFunction<unknown>(
                    'registerForUpdateAvailableNotification',
                    createDotNetObjectReference({
                        OnUpdateAvailable: () => {
                            setNewVersionAvailable(true);
                            return Promise.resolve();
                        },
                    }),
                    'OnUpdateAvailable');
            } catch (e) {
                logger.logError(e, 'Error detecting the notifications.');
            }
        })());
    }, []);

    if (!newVersionAvailable) return null;

    return (
        <li className="nav-item nav-item-dropdown-lg dropdown ms-lg-2">
            <a href="#" className="navbar-nav-link navbar-nav-link-icon rounded-pill" data-bs-toggle="dropdown" data-bs-auto-close="outside" aria-expanded="false">
                <i className="ph-bell"></i>
                <span className="badge bg-yellow text-black position-absolute top-0 end-0 translate-middle-top zindex-1 rounded-pill mt-1 me-1">1</span>
            </a>

            <div className="dropdown-menu dropdown-menu-right wmin-lg-400 p-0">
                <div className="d-flex align-items-center p-3 text-success">
                    <h6 className="mb-0">Есть обновление!</h6>
                </div><div className="d-flex align-items-center p-3 pt-0">
                    <span className="mb-0">Вышла новая версия приложения. Обновите страницу, чтобы её использовать.</span>
                </div>
                <div className="d-flex py-2 px-3 my-link" ref={setInlineOnClick}>
                    <h6 {...{ href: '#' }} className="text-body ms-auto mb-1">Обновить<i className="ph-arrow-circle-right ms-1"></i>
                    </h6>
                </div>
            </div>
        </li>
    );
}
