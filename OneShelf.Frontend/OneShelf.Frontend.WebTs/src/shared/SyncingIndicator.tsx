// The original: Shared/SyncingIndicator.razor.

import { blazorEventHandler } from '../framework/blazorEventHandler';
import { useRenderOnEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { services } from '../services/registry';

export function SyncingIndicator() {
    const collectionIndexProvider = services.collectionIndexProvider;
    const collectionNavigation = services.collectionNavigation;
    const identityProvider = services.identityProvider;
    const stateHasChanged = useStateHasChanged();

    // OnInitializedAsync: CollectionIndexProvider.SyncingChanged += StateHasChanged; IdentityProvider.IdentityChange += StateHasChanged;
    useRenderOnEvent(collectionIndexProvider.syncingChanged);
    useRenderOnEvent(identityProvider.identityChange);

    const sync = (): void => {
        collectionNavigation.synced();
        collectionIndexProvider.sync(true);
    };

    if (identityProvider.identity === null) return null;

    return (
        <li className="nav-item ms-lg-2">
            <a className="navbar-nav-link navbar-nav-link-icon rounded-pill my-link" onClick={blazorEventHandler(stateHasChanged, sync)}>
                <i className={`ph-arrows-clockwise ${collectionIndexProvider.isSyncing ? 'my-rotating' : 'opacity-50'}`}></i>
            </a>
        </li>
    );
}
