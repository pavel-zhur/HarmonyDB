// A guest's writing action: the server answers 401 sign_in_required; the app offers signing in and, once signed in,
// does the action. The action is remembered by name and arguments (session storage), because signing in at a
// provider leaves the page and comes back; actions that may be repeated so are registered by name (data/actions.ts).

import { useSyncExternalStore } from 'react';
import type { en } from '../i18n/en';

export type SignInWhat = keyof typeof en.signIn.what;

interface Offer {
    what: SignInWhat | null;
}

let offer: Offer | null = null;
const listeners = new Set<() => void>();
const emit = () => listeners.forEach(listener => listener());

/** Opens the sign-in dialog, telling what it is for (null: just signing in — then no action waits for it). */
export function offerSignIn(what: SignInWhat | null = null): void {
    if (what === null) forgetPending();
    offer = { what };
    emit();
}

export function closeSignIn(): void {
    offer = null;
    emit();
}

export function useSignInOffer(): Offer | null {
    return useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => offer);
}

const pendingKey = 'shelf.pending';
/** How long an action waits for its sign-in: a provider's round trip; an abandoned one is not done at a later sign-in. */
const pendingLife = 15 * 60 * 1000;
const runners = new Map<string, (args: never) => Promise<unknown>>();

export function registerPending<A>(name: string, run: (args: A) => Promise<unknown>): void {
    runners.set(name, run as (args: never) => Promise<unknown>);
}

export function rememberPending(name: string, args: unknown): void {
    try {
        sessionStorage.setItem(pendingKey, JSON.stringify({ name, args, at: Date.now() }));
    } catch {
        // no storage: the action is not repeated after signing in
    }
}

export function forgetPending(): void {
    try {
        sessionStorage.removeItem(pendingKey);
    } catch {
        // nothing kept
    }
}

/** After signing in: does the remembered action, once. */
export async function runPending(): Promise<void> {
    let pending: { name: string; args: never; at: number } | null = null;
    try {
        pending = JSON.parse(sessionStorage.getItem(pendingKey) ?? 'null');
    } catch {
        pending = null;
    }
    forgetPending();
    const run = pending && Date.now() - pending.at < pendingLife ? runners.get(pending.name) : undefined;
    if (run) await run(pending!.args).catch(() => undefined);
}
