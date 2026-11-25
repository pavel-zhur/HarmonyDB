// A writing action of the API, made once in one place:
//
//   export const leavePlaylist = action('leavePlaylist', 'change',
//       (id: string) => call(api.POST('/api/playlists/{id}/leave', { params: { path: { id } } })),
//       { done: () => t('playlist.left'), undo: r => r.undo, invalidate: () => [['playlists'], ['library']] });
//
// What it does around the call:
// - a guest (401 sign_in_required): offers signing in for `what`, remembers the action and does it after signing in;
// - another error: says it in a toast (worded from its code) and rejects;
// - done: refreshes the queries named by `invalidate`, and says `done` in a toast — with "Undo" when the server gave
//   an undo token; Undo calls /api/sync/undo and refreshes the same queries.
// In a component: useMutation({ mutationFn: leavePlaylist }) for the busy state.

import type { QueryKey } from '@tanstack/react-query';
import { ApiError, api, call } from '../api/client';
import { errorText } from '../api/errors';
import { i18n } from '../i18n';
import { toast } from '../app/toasts';
import { queryClient } from './queryClient';
import { offerSignIn, registerPending, rememberPending, type SignInWhat } from './signInOffer';
import { isGuestPreview } from '../app/guestMode';

export interface ActionOptions<A, R> {
    done?: (result: R, args: A) => string | null;
    undo?: (result: R) => string | null | undefined;
    invalidate?: (result: R, args: A) => QueryKey[];
    /** queries of what is gone now (a playlist left or deleted): dropped, not asked again */
    remove?: (result: R, args: A) => QueryKey[];
    /** after Undo, before anything is read anew: where the person should be now (a playlist made and undone: not on its
     * address) */
    undone?: (result: R, args: A) => void | Promise<void>;
}

export function action<A, R>(name: string, what: SignInWhat, run: (args: A) => Promise<R>, options: ActionOptions<A, R> = {}): (args: A) => Promise<R> {
    const perform = async (args: A): Promise<R> => {
        let result: R;
        // the guest's preview writes nothing: it is asked to sign in, as a guest is (and says it is a preview)
        if (isGuestPreview) {
            offerSignIn(what);
            throw new ApiError(401, 'sign_in_required');
        }
        try {
            result = await run(args);
        } catch (error) {
            if (error instanceof ApiError && error.isSignInRequired) {
                rememberPending(name, args);
                offerSignIn(what);
            } else {
                toast(errorText(error));
            }
            throw error;
        }

        (options.remove?.(result, args) ?? []).forEach(queryKey => queryClient.removeQueries({ queryKey }));
        const keys = options.invalidate?.(result, args) ?? [];
        await Promise.all(keys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
        const text = options.done?.(result, args) ?? null;
        const token = options.undo?.(result) ?? null;
        if (text !== null) toast(text, token ? { undo: () => void undo(token, keys, () => options.undone?.(result, args)) } : {});
        return result;
    };

    registerPending(name, perform);
    return perform;
}

/** Undoes a change of the API by its token; then (after `before`: leaving a page that is gone now) reads anew what it touched. */
export async function undo(token: string, keys: QueryKey[], before?: () => void | Promise<void>): Promise<void> {
    try {
        await call(api.POST('/api/sync/undo', { body: { token } }));
        await before?.();
        await Promise.all(keys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
        toast(i18n.t('local.undone'));
    } catch (error) {
        toast(errorText(error));
    }
}
