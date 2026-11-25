// The person's own account (the profile): the sign-ins (adding one is data/session.ts's addSignIn), merging two
// accounts when a sign-in added is another's, the linked music accounts, wiping and deleting the profile.

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { errorText } from '../api/errors';
import { i18n } from '../i18n';
import { toast } from '../app/toasts';
import { attach, local } from '../local';
import { action } from './actions';
import { closeMerge } from './mergeQuestion';
import { queryClient } from './queryClient';
import { forgetSession, sessionQuery, toastFresh, useSession } from './session';

export type LoginItem = Schemas['LoginItem'];
export type MusicLinkItem = Schemas['MusicLinkItem'];
export type MergeCandidate = Schemas['MergeCandidate'];

const t = i18n.t.bind(i18n);

export function useLogins() {
    const { signedIn } = useSession();
    return useQuery({ queryKey: ['me', 'logins'], queryFn: () => call(api.GET('/api/me/logins')), enabled: signedIn });
}

/** Removing a sign-in needs a recent sign-in: else fresh_sign_in_required (then the way to sign in again). */
export async function removeLogin(id: string): Promise<void> {
    try {
        await call(api.DELETE('/api/me/logins/{id}', { params: { path: { id } } }));
    } catch (error) {
        toastFresh(error);
        throw error;
    }
    await queryClient.invalidateQueries({ queryKey: ['me', 'logins'] });
    toast(t('account.signIns.removed'));
}

/** The other account, as the merge question shows it. */
export function useMergeCandidate(state: string | null) {
    return useQuery({
        queryKey: ['merge', state],
        queryFn: () => call(api.GET('/api/auth/flows/{state}', { params: { path: { state: state! } } })),
        enabled: !!state,
        staleTime: Infinity,
        retry: false,
    });
}

/** The other account merged into this one: everything is read anew, the local-first store too. */
export async function mergeAccounts(state: string): Promise<void> {
    try {
        await call(api.POST('/api/auth/flows/{state}/merge', { params: { path: { state } } }));
    } catch (error) {
        toastFresh(error);
        throw error;
    }
    closeMerge();
    await queryClient.resetQueries();
    await local.exchange();
    toast(t('account.merge.done'));
}

/**
 * Links a music account (in place of the one linked before): the service checks the credentials first. Its refusal
 * is thrown (ApiError) for the dialog to say in place.
 */
export async function linkMusic(service: Schemas['MusicService'], credentials: string): Promise<void> {
    await call(api.PUT('/api/me/music-links/{service}', { params: { path: { service } }, body: { credentials } }));
    await Promise.all([['me', 'music-links'], ['music']].map(queryKey => queryClient.invalidateQueries({ queryKey })));
    toast(t('account.music.linked'));
}

export const unlinkMusic = action('unlinkMusic', 'change',
    (service: Schemas['MusicService']) => call(api.DELETE('/api/me/music-links/{service}', { params: { path: { service } } })),
    { done: () => t('account.music.unlinked'), invalidate: () => [['me', 'music-links'], ['music']] });

/** Songs, playlists, marks, keys, notes go; the profile, its sign-ins and settings stay. */
export async function wipeProfile(): Promise<void> {
    try {
        await call(api.POST('/api/me/wipe'));
    } catch (error) {
        toast(errorText(error));
        throw error;
    }
    await queryClient.resetQueries();
    await local.exchange();
    toast(t('account.danger.wiped'));
}

/** The profile deleted: the browser is a guest after it. */
export async function deleteProfile(): Promise<void> {
    try {
        await call(api.DELETE('/api/me'));
    } catch (error) {
        toast(errorText(error));
        throw error;
    }
    // a guest from now: the screens stop asking what only a person may read, then everything is read anew
    forgetSession();
    queryClient.setQueryData(sessionQuery.queryKey, { accountId: null, permissions: [] });
    queryClient.removeQueries({ predicate: query => query.queryKey[0] !== 'session' });
    attach(null);
}
