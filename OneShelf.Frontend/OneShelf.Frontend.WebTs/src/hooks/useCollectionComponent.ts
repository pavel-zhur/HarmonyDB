// The original: Components/CollectionComponentBase.cs — what a page or component inheriting it gets:
//
//     @inherits CollectionComponentBase
//     protected override async Task OnNewCollectionIndexReceived() { ... }
//
// becomes
//
//     const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(async () => { ... });
//
// - collectionIndex / collectionIndexFailed are CollectionIndex / CollectionIndexFailed, read at each render
//   (CollectionIndexProvider.Peek());
// - on CollectionChanged the given OnNewCollectionIndexReceived runs, then the component renders again; the provider
//   waits for both (its event awaits its handlers);
// - when the component appears, if the index has failed, a sync is started (OnInitializedAsync);
// - retryCollectionIndex is RetryCollectionIndex().

import { useLayoutEffect } from 'react';
import type { CollectionIndex } from '../models/CollectionIndex';
import { services } from '../services/registry';
import { useAsyncEvent } from './useEvent';
import { useStateHasChanged } from './useStateHasChanged';

export interface CollectionComponent {
    readonly collectionIndex: CollectionIndex | null;
    readonly collectionIndexFailed: boolean;
    readonly retryCollectionIndex: () => void;
}

export function useCollectionComponent(onNewCollectionIndexReceived?: () => Promise<void> | void): CollectionComponent {
    const collectionIndexProvider = services.collectionIndexProvider;
    const stateHasChanged = useStateHasChanged();

    useAsyncEvent(collectionIndexProvider.collectionChanged, async (_collectionIndex: CollectionIndex | null, _failed: boolean) => {
        await onNewCollectionIndexReceived?.();
        stateHasChanged();
    });

    useLayoutEffect(() => {
        if (collectionIndexProvider.peek().failed) collectionIndexProvider.sync();
    }, [collectionIndexProvider]);

    const { collectionIndex, failed } = collectionIndexProvider.peek();
    return {
        collectionIndex,
        collectionIndexFailed: failed,
        retryCollectionIndex: () => collectionIndexProvider.sync(),
    };
}
