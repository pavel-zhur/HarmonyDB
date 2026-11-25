// Who the browser is, how one signs in and out, the server's public configuration.

import { useQuery } from '@tanstack/react-query';
import { ApiError, api, call, type Schemas } from '../api/client';
import { errorText } from '../api/errors';
import { i18n } from '../i18n';
import { toast } from '../app/toasts';
import { queryClient } from './queryClient';
import { closeSignIn, forgetPending, runPending } from './signInOffer';
import { attach } from '../local/store';

export const configurationQuery = {
    queryKey: ['configuration'],
    queryFn: () => call(api.GET('/api/app/configuration')),
    staleTime: Infinity,
};

export function useConfiguration() {
    return useQuery(configurationQuery);
}

export const sessionQuery = {
    queryKey: ['session'],
    queryFn: () => call(api.GET('/api/auth/session')),
    staleTime: 60_000,
};

/** The session: `signedIn` once known; `accountId` of the person (null: a guest); `error` when the server cannot say. */
export function useSession(): { known: boolean; signedIn: boolean; accountId: string | null; session?: Schemas['CurrentSession']; error: unknown } {
    const { data, error } = useQuery(sessionQuery);
    return { known: data !== undefined, signedIn: !!data?.accountId, accountId: data?.accountId ?? null, session: data, error };
}

export function useProfile() {
    const { signedIn } = useSession();
    return useQuery({ queryKey: ['me'], queryFn: () => call(api.GET('/api/me')), enabled: signedIn, staleTime: 5 * 60_000 });
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
        const start = await call(api.POST('/api/auth/flows', { body: { provider, purpose: 'signIn', returnPath: here() } }));
        if (start.redirectUrl) {
            location.assign(start.redirectUrl);
            return;
        }
        await call(api.POST('/api/auth/flows/{state}/complete', {
            params: { path: { state: start.state } },
            // a name: a test person; an account's id: that existing account, as it is
            body: { answer: uuid.test(devName ?? '') ? { accountId: devName! } : { name: devName ?? '' } },
        }));
        await signedIn();
    } catch (error) {
        toast(errorText(error));
    }
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

/** Back from a provider: ?auth=<outcome or error code>&auth_flow=<state>. Called once at start. */
export async function finishSignInFromAddress(): Promise<void> {
    const url = new URL(location.href);
    const outcome = url.searchParams.get('auth');
    if (outcome === null) return;
    url.searchParams.delete('auth');
    url.searchParams.delete('auth_flow');
    history.replaceState(history.state, '', url.pathname + url.search + url.hash);
    if (outcome === 'signedIn' || outcome === 'linked' || outcome === 'alreadyLinked') await signedIn();
    else {
        forgetPending();
        toast(errorText(new ApiError(0, outcome)));
    }
}

export async function signOut(): Promise<void> {
    try {
        await call(api.POST('/api/auth/sign-out'));
        attach(null);
        await queryClient.resetQueries();
        toast(i18n.t('signIn.signedOut'));
    } catch (error) {
        toast(errorText(error));
    }
}
