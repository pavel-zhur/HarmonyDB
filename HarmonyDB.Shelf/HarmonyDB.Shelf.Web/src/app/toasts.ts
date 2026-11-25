// The toast at the bottom: one at a time, a few seconds; with "Undo" after a change (the miss of a finger), or another
// action of its own. Callable from anywhere, also outside React:
//
//   toast(t('playlist.saved'), { undo: () => … })
//   toast(text, { action: { label, run }, stays: true })

import { useSyncExternalStore } from 'react';

export interface ToastAction {
    label: string;
    run: () => void;
}

export interface Toast {
    id: number;
    text: string;
    undo?: () => void;
    action?: ToastAction;
}

let current: Toast | null = null;
let timer: ReturnType<typeof setTimeout> | undefined;
let nextId = 1;
const listeners = new Set<() => void>();

function emit() {
    listeners.forEach(listener => listener());
}

export function toast(text: string, options: { undo?: () => void; action?: ToastAction; stays?: boolean } = {}): void {
    clearTimeout(timer);
    current = { id: nextId++, text, undo: options.undo, action: options.action };
    emit();
    if (!options.stays) timer = setTimeout(dismissToast, options.undo || options.action ? 6000 : 3500);
}

export function dismissToast(): void {
    clearTimeout(timer);
    current = null;
    emit();
}

export function useToast(): Toast | null {
    return useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => current);
}
