// The original: Pages/MyCollection.razor.
import { useLayoutEffect } from 'react';
import { navigationManager } from '../framework/navigation';
import { services } from '../services/registry';

export function MyCollection() {
    const collectionNavigation = services.collectionNavigation;

    // OnInitializedAsync
    useLayoutEffect(() => {
        navigationManager.navigateTo(collectionNavigation.getExternalNavigationLinkMyRatingNewest());
    }, []);

    return <h3>MyCollection</h3>;
}
