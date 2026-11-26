// "Your AI": the shelf's MCP server — its address and what the person may let an assistant do; the assistants
// connected (what each may do, changed or revoked); personal keys for programs that cannot sign in; and the consent
// an assistant's sign-in brings the person to (/connect?request=…).
//
// The permissions are the server's MCP permissions (mcp.index, mcp.library.read …), each worded by the app
// (i18n ai.permission.<key with "_" for ".">); the order is the server's.

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { i18n } from '../i18n';
import { action } from './actions';
import { toastFresh, useSession } from './session';
import { queryClient } from './queryClient';
import { toast } from '../app/toasts';

export type ConnectedClient = Schemas['ConnectedClient'];
export type PersonalKey = Schemas['PersonalKeyItem'];
export type AuthorizationRequestView = Schemas['AuthorizationRequestView'];

const t = i18n.t.bind(i18n);

export function useMcpOverview() {
    const { signedIn } = useSession();
    return useQuery({ queryKey: ['mcp'], queryFn: () => call(api.GET('/api/mcp')), enabled: signedIn });
}

/** Whether the person may let an assistant do anything (the server's word: what they may grant); a guest may not. */
export function useAssistantsOpen(): boolean {
    return !!useMcpOverview().data?.grantable.length;
}

export function useMcpClients() {
    const { signedIn } = useSession();
    return useQuery({ queryKey: ['mcp', 'clients'], queryFn: () => call(api.GET('/api/mcp/clients')), enabled: signedIn });
}

export function usePersonalKeys() {
    const { signedIn } = useSession();
    return useQuery({ queryKey: ['mcp', 'keys'], queryFn: () => call(api.GET('/api/mcp/keys')), enabled: signedIn });
}

/**
 * What a connected assistant may do: less holds at once; more needs a recent sign-in (else fresh_sign_in_required, with
 * the way to sign in again), and then signs the assistant out everywhere.
 */
export async function setClientPermissions({ id, permissions }: { id: string; permissions: string[] }) {
    try {
        const client = await call(api.PUT('/api/mcp/clients/{id}', { params: { path: { id } }, body: { permissions } }));
        await queryClient.invalidateQueries({ queryKey: ['mcp', 'clients'] });
        toast(t('ai.clients.changed'));
        return client;
    } catch (error) {
        toastFresh(error);
        throw error;
    }
}

export const disconnectClient = action('disconnectClient', 'change',
    (id: string) => call(api.DELETE('/api/mcp/clients/{id}', { params: { path: { id } } })),
    { done: () => t('ai.clients.disconnected'), invalidate: () => [['mcp', 'clients']] });

/** A new key: its secret is in the answer only, shown once. */
export const createPersonalKey = action('createPersonalKey', 'change',
    (body: Schemas['CreatePersonalKeyRequest']) => call(api.POST('/api/mcp/keys', { body })),
    { invalidate: () => [['mcp', 'keys']] });

export const revokePersonalKey = action('revokePersonalKey', 'change',
    (id: string) => call(api.DELETE('/api/mcp/keys/{id}', { params: { path: { id } } })),
    { done: () => t('ai.keys.revoked'), invalidate: () => [['mcp', 'keys']] });

/**
 * The pending request of an assistant, by the id in the address. Opening it binds it to the person who opened it
 * first; it is read once (never again in the background: a second reading after the decision answers "decided").
 */
export function useAuthorizationRequest(id: string | null) {
    const { signedIn } = useSession();
    return useQuery({
        queryKey: ['oauth-request', id],
        queryFn: () => call(api.GET('/api/oauth/requests/{id}', { params: { path: { id: id! } } })),
        enabled: signedIn && !!id,
        staleTime: Infinity,
        gcTime: 0,
        retry: false,
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
    });
}

/** The person's answer: where the browser goes next (back to the assistant, with a code or a refusal). */
export function approveRequest(id: string, permissions: string[]) {
    return call(api.POST('/api/oauth/requests/{id}/approve', { params: { path: { id } }, body: { permissions } }));
}

export function denyRequest(id: string) {
    return call(api.POST('/api/oauth/requests/{id}/deny', { params: { path: { id } } }));
}

/** The i18n key of a permission's words. */
export const permissionKey = (permission: string) => `ai.permission.${permission.replace(/\./g, '_')}` as const;
