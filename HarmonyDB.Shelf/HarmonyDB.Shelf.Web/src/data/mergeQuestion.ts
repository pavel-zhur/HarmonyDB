// A sign-in being added that is another account's: the flow's state, until the person answers whether to merge the
// two accounts (screens/profile/MergeDialog.tsx asks; data/account.ts merges).

import { useSyncExternalStore } from 'react';

let state: string | null = null;
const listeners = new Set<() => void>();
const emit = () => listeners.forEach(listener => listener());

export function askMerge(flowState: string): void {
    state = flowState;
    emit();
}

export function closeMerge(): void {
    state = null;
    emit();
}

export function useMergeQuestion(): string | null {
    return useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => state);
}
