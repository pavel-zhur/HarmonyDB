// "The song one came back from is lit": per list, the row opened last (kept while the app is open).

import { useSyncExternalStore } from 'react';

const last = new Map<string, string>();
const listeners = new Set<() => void>();

export function setLastOpened(list: string, rowId: string): void {
    if (last.get(list) === rowId) return;
    last.set(list, rowId);
    listeners.forEach(listener => listener());
}

export function useLastOpened(list: string): string | null {
    return useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => last.get(list) ?? null);
}
