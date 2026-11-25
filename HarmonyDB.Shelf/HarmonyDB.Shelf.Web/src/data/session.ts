// Who the browser is, how one signs in and out, the server's public configuration.

import { useQuery } from '@tanstack/react-query';
import { ApiError, api, call, type Schemas } from '../api/client';
import { errorText } from '../api/errors';
import { i18n } from '../i18n';
import { toast } from '../app/toasts';
import { queryClient } from './queryClient';
import { closeSignIn, forgetPending, offerSignIn, runPending } from './signInOffer';
import { askMerge } from './mergeQuestion';
import { attach } from '../local/store';

const lastConfigurationKey = 'shelf.lastConfiguration';

/**
 * The server's public configuration (sign-in providers, the kinds of marks …), remembered in this browser: with no
 * connection the last one is used — marks can be put offline too.
 */
export const configurationQuery = {
    queryKey: ['configuration'],
    queryFn: async () => {
        try {
            const configuration = await call(api.GET('/api/app/configuration'));
            remember(lastConfigurationKey, configuration);
            return configuration;
        } catch (error) {
            const last = isNetwork(error) ? remembered<Schemas['PublicConfiguration']>(lastConfigurationKey) : null;
            if (!last) throw error;
            return last;
        }
    },
    staleTime: Infinity,
    networkMode: 'always' as const,
};

export function useConfiguration() {
    return useQuery(configurationQuery);
}

// What this browser remembers of who it is, for when there is no connection: the server's last word on the session and
// the person's name and picture. Signing out forgets it at once; a sign-out made with no connection is told to the
// server when the connection is back, before anything else is asked.
const lastSessionKey = 'shelf.lastSession';
const lastProfileKey = 'shelf.lastProfile';
const signOutPendingKey = 'shelf.signOutPending';

function remember(key: string, value: unknown): void {
    try {
        if (value === null) localStorage.removeItem(key);
        else localStorage.setItem(key, JSON.stringify(value));
    } catch {
        // no storage: nothing remembered
    }
}

function remembered<T>(key: string): T | null {
    try {
        return JSON.parse(localStorage.getItem(key) ?? 'null') as T | null;
    } catch {
        return null;
    }
}

const isNetwork = (error: unknown) => error instanceof ApiError && error.code === 'network';

/**
 * A sign-out made offline, told to the server now. The mark stays until the server confirms the session is ended (its
 * yes, or 401: no session any more); with no connection or any other answer (the server failing, too many tries) it is
 * told again later, and meanwhile the browser stays a guest. True once confirmed.
 */
export async function finishSignOut(): Promise<boolean> {
    if (!remembered<boolean>(signOutPendingKey)) return true;
    try {
        await call(api.POST('/api/auth/sign-out'));
    } catch (error) {
        if (!(error instanceof ApiError && error.status === 401)) return false;
    }
    remember(signOutPendingKey, null);
    return true;
}

/**
 * Who the browser is: the server's word, remembered; with no connection, the word it said last (the person goes on
 * offline with the library kept on the device — offline/index.ts), or a guest after an offline sign-out.
 */
async function readSession(): Promise<Schemas['CurrentSession']> {
    const guest: Schemas['CurrentSession'] = { accountId: null, permissions: [] };
    if (!(await finishSignOut())) return guest;
    try {
        const session = await call(api.GET('/api/auth/session'));
        remember(lastSessionKey, session);
        if (!session.accountId || remembered<Schemas['Profile']>(lastProfileKey)?.id !== session.accountId) remember(lastProfileKey, null);
        return session;
    } catch (error) {
        const last = isNetwork(error) ? remembered<Schemas['CurrentSession']>(lastSessionKey) : null;
        if (!last) throw error;
        return last;
    }
}

if (typeof addEventListener === 'function') {
    addEventListener('online', () => {
        if (remembered<boolean>(signOutPendingKey)) void finishSignOut();
    });
}

export const sessionQuery = {
    queryKey: ['session'],
    queryFn: readSession,
    staleTime: 60_000,
    networkMode: 'always' as const,
};

/** The session: `signedIn` once known; `accountId` of the person (null: a guest); `error` when the server cannot say. */
export function useSession(): { known: boolean; signedIn: boolean; accountId: string | null; session?: Schemas['CurrentSession']; error: unknown } {
    const { data, error } = useQuery(sessionQuery);
    return { known: data !== undefined, signedIn: !!data?.accountId, accountId: data?.accountId ?? null, session: data, error };
}

/** The person's profile; with no connection, the one remembered for this account. */
export function useProfile() {
    const { signedIn, accountId } = useSession();
    return useQuery({
        queryKey: ['me'],
        queryFn: async () => {
            try {
                const profile = await call(api.GET('/api/me'));
                remember(lastProfileKey, profile);
                return profile;
            } catch (error) {
                const last = isNetwork(error) ? remembered<Schemas['Profile']>(lastProfileKey) : null;
                if (!last || last.id !== accountId) throw error;
                return last;
            }
        },
        enabled: signedIn,
        staleTime: 5 * 60_000,
        networkMode: 'always',
    });
}

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** The address to come back to after signing in at a provider: where the person is now. */
function here(): string {
    return location.pathname + location.search;
}

/**
 * Starts signing in with a provider. One that sends the browser away (Telegram, Google, Yandex) comes back to this
 * address with ?auth=…; the dev provider is answered here, with the test person's name.
 */
export async function signIn(provider: string, devName?: string): Promise<void> {
    try {
        if (await runFlow(provider, 'signIn', devName)) await signedIn();
    } catch (error) {
        toast(errorText(error));
    }
}

/**
 * Adds a sign-in of a provider to the account (the profile). As signing in: a provider that sends the browser away
 * comes back here with the outcome; the dev provider is answered here. A sign-in that is another account's asks the
 * person whether to merge the two (data/account.ts).
 */
export async function addSignIn(provider: string, devName?: string): Promise<void> {
    try {
        const result = await runFlow(provider, 'link', devName);
        if (result) await linked(result.outcome, result.state);
    } catch (error) {
        toastFresh(error);
    }
}

/**
 * An error's toast; for fresh_sign_in_required (adding or removing a sign-in, merging: the session's sign-in is not
 * recent) with the way to sign in again, after which the person repeats the step.
 */
export function toastFresh(error: unknown): void {
    if (error instanceof ApiError && error.code === 'fresh_sign_in_required') {
        toast(errorText(error), { action: { label: i18n.t('account.signIns.again'), run: () => offerSignIn() }, stays: true });
    } else {
        toast(errorText(error));
    }
}

/** A flow started; answered here (dev): its outcome. Sent to a provider: null (the browser leaves). */
async function runFlow(provider: string, purpose: Schemas['SignInPurpose'], devName?: string): Promise<{ outcome: Schemas['SignInOutcome']; state: string } | null> {
    const start = await call(api.POST('/api/auth/flows', { body: { provider, purpose, returnPath: here() } }));
    if (start.redirectUrl) {
        location.assign(start.redirectUrl);
        return null;
    }
    const result = await call(api.POST('/api/auth/flows/{state}/complete', {
        params: { path: { state: start.state } },
        // a name: a test person; an account's id: that existing account, as it is
        body: { answer: uuid.test(devName ?? '') ? { accountId: devName! } : { name: devName ?? '' } },
    }));
    return { outcome: result.outcome, state: start.state };
}

/** A sign-in added: the profile's list read anew; one that is another account's: the question of merging. */
async function linked(outcome: Schemas['SignInOutcome'], state: string): Promise<void> {
    if (outcome === 'mergeNeeded') {
        askMerge(state);
        return;
    }
    await queryClient.invalidateQueries({ queryKey: ['me'] });
    toast(i18n.t(outcome === 'alreadyLinked' ? 'account.signIns.already' : 'account.signIns.added'));
}

/** After signing in, here or back from a provider: everything read anew as the person, then the action they wanted. */
async function signedIn(): Promise<void> {
    closeSignIn();
    // everything read anew as the person (the screens stay subscribed to their queries); the local-first store follows
    // the session before the action wanted is done
    await queryClient.resetQueries();
    const session = await queryClient.fetchQuery({ ...sessionQuery, staleTime: 0 });
    attach(session.accountId ?? null);
    toast(i18n.t('signIn.signedIn'));
    await runPending();
}

/** The outcomes as the provider's way back names them in the address. */
const outcomes: Record<string, Schemas['SignInOutcome']> = { signed_in: 'signedIn', linked: 'linked', already_linked: 'alreadyLinked', merge_needed: 'mergeNeeded' };

/** Back from a provider: ?auth=<outcome or error code>&auth_flow=<state>. Called once at start. */
export async function finishSignInFromAddress(): Promise<void> {
    const url = new URL(location.href);
    const code = url.searchParams.get('auth');
    if (code === null) return;
    const state = url.searchParams.get('auth_flow');
    url.searchParams.delete('auth');
    url.searchParams.delete('auth_flow');
    history.replaceState(history.state, '', url.pathname + url.search + url.hash);
    const outcome = outcomes[code];
    if (outcome === 'signedIn') await signedIn();
    else if (outcome) await linked(outcome, state ?? '');
    else {
        forgetPending();
        toast(errorText(new ApiError(0, code)));
    }
}

/**
 * Signing out. With no connection it is done on this device at once — the library's copy, the remembered session and
 * profile go — and told to the server when the connection is back.
 */
export async function signOut(): Promise<void> {
    try {
        await call(api.POST('/api/auth/sign-out'));
    } catch (error) {
        if (!isNetwork(error)) {
            toast(errorText(error));
            return;
        }
        remember(signOutPendingKey, true);
    }
    remember(lastSessionKey, null);
    remember(lastProfileKey, null);
    attach(null);
    await queryClient.resetQueries();
    toast(i18n.t('signIn.signedOut'));
}
