// Playlists: reading, and every change of them (each an action: data/actions.ts).

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { keys, readThrough } from '../offline';
import { i18n } from '../i18n';
import { action } from './actions';
import { queryClient } from './queryClient';
import { useSession } from './session';

export type PlaylistSummary = Schemas['PlaylistSummary'];
export type PlaylistDetails = Schemas['PlaylistDetails'];
export type PlaylistRole = Schemas['PlaylistRole'];

const t = i18n.t.bind(i18n);

/** Goes to an address from outside the screens (the router is loaded with them). */
async function goTo(path: string): Promise<void> {
    const { router } = await import('../app/routes');
    await router.navigate(path);
}

export function useMyPlaylists() {
    const { signedIn } = useSession();
    return useQuery({ queryKey: ['playlists', 'mine'], queryFn: () => readThrough(keys.myPlaylists, () => call(api.GET('/api/playlists'))), enabled: signedIn, networkMode: 'always' });
}

export function usePublicPlaylists(q: string, limit = 100) {
    const text = q.trim();
    return useQuery({
        queryKey: ['playlists', 'public', text, limit],
        queryFn: () => call(api.GET('/api/playlists/public', { params: { query: { q: text || undefined, limit } } })),
    });
}

export function usePlaylist(id: string, share?: string | null) {
    return useQuery({
        queryKey: ['playlist', id, share ?? null],
        queryFn: () => readThrough(keys.playlist(id), () => call(api.GET('/api/playlists/{id}', { params: { path: { id }, query: { share: share ?? undefined } } }))),
        networkMode: 'always',
    });
}

export function useInvitation(token: string) {
    return useQuery({
        queryKey: ['invitation', token],
        queryFn: () => call(api.GET('/api/playlists/invitations/{token}', { params: { path: { token } } })),
    });
}

/** A playlist's name as shown: the system playlists are named by the app, in the person's language. */
export function playlistTitle(playlist: Pick<PlaylistSummary, 'title' | 'systemKind'>): string {
    return playlist.systemKind === 'none' ? playlist.title : t(`playlists.system.${playlist.systemKind}`);
}

const lists = (id?: string) => [['playlists'], ['songs'], ...(id ? [['playlist', id]] : [])];

export const createPlaylist = action('createPlaylist', 'create',
    (title: string) => call(api.POST('/api/playlists', { body: { title, access: 'private', addPolicy: 'allMembers' } })),
    {
        done: () => t('playlists.created'), undo: r => r.undo, invalidate: () => lists(),
        undone: async r => {
            if (location.pathname.startsWith(`/playlists/${r.value.playlist.id}`)) await goTo('/playlists');
            queryClient.removeQueries({ queryKey: ['playlist', r.value.playlist.id] });
        },
    });

export const showInLibrary = action('showInLibrary', 'change',
    ({ id, show }: { id: string; show: boolean }) => call(api.PUT('/api/playlists/{id}/library', { params: { path: { id } }, body: { show } })),
    { done: (_, { show }) => t(show ? 'playlist.shownInLibrary' : 'playlist.hiddenFromLibrary'), undo: r => r.undo, invalidate: (_, { id }) => lists(id) });

/** A public playlist (or one open by its link) kept in my library without being its member; and let go. */
export const followPlaylist = action('followPlaylist', 'change',
    ({ id, share }: { id: string; share?: string | null }) =>
        call(api.POST('/api/playlists/{id}/follow', { params: { path: { id }, query: { share: share ?? undefined } } })),
    { done: () => t('playlist.shownInLibrary'), undo: r => r.undo, invalidate: (_, { id }) => lists(id) });

export const unfollowPlaylist = action('unfollowPlaylist', 'change',
    (id: string) => call(api.DELETE('/api/playlists/{id}/follow', { params: { path: { id } } })),
    { done: () => t('playlist.hiddenFromLibrary'), undo: r => r.undo, invalidate: (_, id) => lists(id) });

export type PlaylistPatch = Omit<Schemas['UpdatePlaylistRequest'], 'clearSearchFirst'> & { clearSearchFirst?: boolean };

export const updatePlaylist = action('updatePlaylist', 'change',
    ({ id, patch }: { id: string; patch: PlaylistPatch }) =>
        call(api.PATCH('/api/playlists/{id}', { params: { path: { id } }, body: { clearSearchFirst: false, ...patch } })),
    { done: () => t('playlist.saved'), undo: r => r.undo, invalidate: (_, { id }) => lists(id) });

export const deletePlaylist = action('deletePlaylist', 'change',
    (id: string) => call(api.DELETE('/api/playlists/{id}', { params: { path: { id } } })),
    { done: () => t('playlist.deleted'), undo: r => r.undo, invalidate: () => lists(), remove: (_, id) => [['playlist', id], ['songs', { kind: 'playlist', id }]] });

export const leavePlaylist = action('leavePlaylist', 'change',
    (id: string) => call(api.POST('/api/playlists/{id}/leave', { params: { path: { id } } })),
    { done: () => t('playlist.left'), undo: r => r.undo, invalidate: () => lists(), remove: (_, id) => [['playlist', id]] });

export const clearMine = action('clearMine', 'change',
    ({ id, added }: { id: string; added: boolean }) =>
        call(api.DELETE('/api/playlists/{id}/mine', { params: { path: { id }, query: { added } } })),
    { done: r => t(r.undo ? 'playlist.cleared' : 'playlist.nothingToClear'), undo: r => r.undo, invalidate: (_, { id }) => lists(id) });

export const setMemberRole = action('setMemberRole', 'change',
    ({ id, accountId, role }: { id: string; accountId: string; role: PlaylistRole }) =>
        call(api.PUT('/api/playlists/{id}/members/{accountId}', { params: { path: { id, accountId } }, body: { role } })),
    { done: () => t('playlist.saved'), undo: r => r.undo, invalidate: (_, { id }) => lists(id) });

export const removeMember = action('removeMember', 'change',
    ({ id, accountId }: { id: string; accountId: string }) =>
        call(api.DELETE('/api/playlists/{id}/members/{accountId}', { params: { path: { id, accountId } } })),
    { done: () => t('playlist.saved'), undo: r => r.undo, invalidate: (_, { id }) => lists(id) });

export const passOwnership = action('passOwnership', 'change',
    ({ id, accountId }: { id: string; accountId: string }) =>
        call(api.POST('/api/playlists/{id}/owner', { params: { path: { id } }, body: { accountId } })),
    { done: () => t('playlist.saved'), undo: r => r.undo, invalidate: (_, { id }) => lists(id) });

export const createInvitation = action('createInvitation', 'change',
    ({ id, role, expiresInDays, maxUses }: { id: string; role: PlaylistRole; expiresInDays: number; maxUses: number | null }) =>
        call(api.POST('/api/playlists/{id}/invitations', { params: { path: { id } }, body: { role, expiresInDays, maxUses } })),
    { done: () => t('playlist.invitationMade'), undo: r => r.undo, invalidate: (_, { id }) => [['playlist', id]] });

export const revokeInvitation = action('revokeInvitation', 'change',
    ({ id, token }: { id: string; token: string }) =>
        call(api.DELETE('/api/playlists/{id}/invitations/{token}', { params: { path: { id, token } } })),
    { done: () => t('playlist.revoked'), undo: r => r.undo, invalidate: (_, { id }) => [['playlist', id]] });

export const acceptInvitation = action('acceptInvitation', 'join',
    (token: string) => call(api.POST('/api/playlists/invitations/{token}/accept', { params: { path: { token } } })),
    { done: r => t('invitation.joined', { title: playlistTitle(r.value.playlist) }), undo: r => r.undo, invalidate: (_, token) => [...lists(), ['invitation', token]] });

export const importPlaylist = action('importPlaylist', 'import',
    (url: string) => call(api.POST('/api/playlists/import', { body: { url } })),
    { done: () => t('importPlaylist.done'), undo: r => r.undo, invalidate: () => lists() });

/** Starts the update of an imported playlist from its service (a job: the details carry it in `sync`). */
export const syncPlaylist = action('syncPlaylist', 'change',
    (id: string) => call(api.POST('/api/playlists/{id}/sync', { params: { path: { id } } })),
    { invalidate: (_, id) => [['playlist', id]] });

/** A job of the person's, followed while it is queued or running. */
export function useJob(id: string | null | undefined) {
    return useQuery({
        queryKey: ['job', id],
        queryFn: () => call(api.GET('/api/jobs/{id}', { params: { path: { id: id! } } })),
        enabled: !!id,
        refetchInterval: query => (query.state.data && !['queued', 'running'].includes(query.state.data.status) ? false : 1500),
    });
}

/** The address of a playlist's link (for a playlist of `link` access): the same link every time it is asked. */
export async function playlistLink(id: string): Promise<string> {
    const link = await call(api.POST('/api/share', { body: { kind: 'playlist', playlistId: id, carriesAccess: false } }));
    return `${location.origin}/s/${link.token}`;
}
