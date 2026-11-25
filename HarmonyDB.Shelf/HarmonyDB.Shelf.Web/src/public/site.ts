// What the public page is told: by its container (docker/public-site.sh writes it into public.html when it starts) the
// app's address, where the page invites; by the server's public configuration the showcase — the open playlist of
// analyses its main page shows.

import { useQuery } from '@tanstack/react-query';
import { api, call } from '../api/client';

/** The app's origin, e.g. https://… (no slash at the end). */
const appOrigin = document.querySelector<HTMLMetaElement>('meta[name="shelf-app"]')?.content ?? '';

/** An address of the app: `appAddress('/chords/ha-…')`. */
export function appAddress(path = '/'): string {
    return appOrigin + path;
}

/** The showcase's playlist: its id, or null when the server names none. */
export function useShowcase() {
    return useQuery({
        queryKey: ['configuration'],
        queryFn: () => call(api.GET('/api/app/configuration')),
        staleTime: Infinity,
        select: configuration => configuration.showcasePlaylistId ?? null,
    });
}
