// The person's settings (profile: sizes, theme, language, chord options) — the server's for a person, kept in this
// browser for a guest. Read with useSettings(), change with updateSettings({ … }): applied at once, sent to the
// server a moment later (a pinch makes many small changes; one is sent).

import { useQuery } from '@tanstack/react-query';
import { api, call, type Schemas } from '../api/client';
import { errorText } from '../api/errors';
import { toast } from '../app/toasts';
import { queryClient } from './queryClient';
import { sessionQuery, useSession } from './session';

export type Settings = Schemas['Settings'];

export const defaultSettings: Settings = {
    locale: null,
    chordsScale: 1,
    interfaceScale: 1,
    lessMode: false,
    accidentals: 'asWritten',
    showDegrees: true,
    chordSuperscripts: true,
    theme: 'system',
};

const guestKey = 'shelf.settings';
const settingsKey = ['settings'];

/** The look this browser had last (app/appearance.ts keeps it, public/boot.js reads it before the app). */
function lastLook(): Settings {
    try {
        const look = JSON.parse(localStorage.getItem('shelf.look') ?? 'null') as Partial<Pick<Settings, 'theme' | 'interfaceScale' | 'locale'>> | null;
        return look ? { ...readGuest(), theme: look.theme ?? 'system', interfaceScale: look.interfaceScale ?? 1, locale: look.locale ?? null } : readGuest();
    } catch {
        return readGuest();
    }
}

function readGuest(): Settings {
    try {
        return { ...defaultSettings, ...JSON.parse(localStorage.getItem(guestKey) ?? '{}') };
    } catch {
        return defaultSettings;
    }
}

function writeGuest(settings: Settings): void {
    try {
        localStorage.setItem(guestKey, JSON.stringify(settings));
    } catch {
        // no storage: kept until the page closes
    }
}

export function useSettings(): Settings {
    const { signedIn, known } = useSession();
    const { data } = useQuery({
        queryKey: [...settingsKey, signedIn],
        queryFn: () => (signedIn ? call(api.GET('/api/me/settings')) : Promise.resolve(readGuest())),
        enabled: known,
        staleTime: Infinity,
    });
    // while they are read: the look this browser had last (public/boot.js put it on already) — no flash of another
    return data ?? lastLook();
}

let sendTimer: ReturnType<typeof setTimeout> | undefined;

function currentKey(): [string, boolean] {
    const signedIn = !!queryClient.getQueryData<Schemas['CurrentSession']>(sessionQuery.queryKey)?.accountId;
    return [settingsKey[0]!, signedIn];
}

/** The settings now, outside React. */
export function readSettings(): Settings {
    return queryClient.getQueryData<Settings>(currentKey()) ?? readGuest();
}

export function updateSettings(patch: Partial<Settings>): void {
    const key = currentKey();
    const signedIn = key[1];
    const next = { ...readSettings(), ...patch };
    queryClient.setQueryData(key, next);
    if (!signedIn) {
        writeGuest(next);
        return;
    }

    clearTimeout(sendTimer);
    sendTimer = setTimeout(() => {
        call(api.PUT('/api/me/settings', { body: queryClient.getQueryData<Settings>(key) ?? next }))
            .then(saved => queryClient.setQueryData(key, saved))
            .catch(error => toast(errorText(error)));
    }, 600);
}
