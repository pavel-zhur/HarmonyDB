// The guest's preview: the whole app, as a guest gets it, inside a frame of the app's own "how a guest sees it" page
// (screens/share/GuestPreview.tsx). Decided once, before anything else of the app runs (main.tsx imports this first),
// and only when this page is framed by that page of the app itself — never by an address alone: a page on its own
// (a link opened, a tab) is never a guest's preview, and other sites cannot frame the app (frame-ancestors 'self').
//
// In the preview, for this page's lifetime:
// - every request to the server goes without the person's credentials (fetch; the API client uses it) — the server
//   answers as to a guest;
// - the browser's storage of the person is neither read nor written: localStorage and sessionStorage are this page's
//   memory, IndexedDB and the Cache API are not there (the app goes on without them, as in a browser that has none);
// - no worker is registered from it (app/pwa.ts), no CSRF token is sent (api/client.ts), and signing in is not
//   offered as working: where a guest would be asked to sign in, the preview says that it is one.
//
// And the rule for any frame: the app, when framed, is only ever the guest's preview. A framed page that is not the
// preview (framed by any other page — of the app or not) refuses to run: a plain line, nothing of the person's, no
// control (main.tsx). So the only framed app there is has no session: no framed click can cost the person anything
// (the MCP consent, removing the profile, keys, merges). The headers that forbid framing (docker/) stand in front of
// this as defence in depth; behind the service worker, which answers navigations with its kept page, they cannot.
//
//   isGuestPreview      the decision
//   framedRefused       framed, and not the preview: the app does not run
//   runsIn(w)           the rules, for a window: 'app' | 'guest' | 'refused' (tested in guestMode.test.ts)

/** The address of the app's page that frames the preview (/s/<token>/guest). */
const previewPage = /^\/s\/[^/]+\/guest$/;

/** Whether this window is the preview: framed, by the same origin, by the app's preview page. */
export function guestPreviewOf(w: Pick<Window, 'parent' | 'location'>): boolean {
    if (w.parent === (w as unknown)) return false;
    try {
        const parent = w.parent.location;
        return parent.origin === w.location.origin && previewPage.test(parent.pathname);
    } catch {
        // a parent of another origin: not the app
        return false;
    }
}

/** How the app may run in a window: 'app' (on its own), 'guest' (the preview), or 'refused' (framed otherwise). */
export function runsIn(w: Pick<Window, 'parent' | 'location'>): 'app' | 'guest' | 'refused' {
    if (w.parent === (w as unknown)) return 'app';
    return guestPreviewOf(w) ? 'guest' : 'refused';
}

const mode = typeof window !== 'undefined' ? runsIn(window) : 'app';
export const isGuestPreview = mode === 'guest';
export const framedRefused = mode === 'refused';

/** A Storage kept in this page's memory. */
export class MemoryStorage implements Storage {
    private readonly items = new Map<string, string>();

    get length(): number {
        return this.items.size;
    }

    key(index: number): string | null {
        return [...this.items.keys()][index] ?? null;
    }

    getItem(key: string): string | null {
        return this.items.get(String(key)) ?? null;
    }

    setItem(key: string, value: string): void {
        this.items.set(String(key), String(value));
    }

    removeItem(key: string): void {
        this.items.delete(String(key));
    }

    clear(): void {
        this.items.clear();
    }
}

/** Puts the preview's rules on a window (its storage, its requests); the window's own browser objects otherwise. */
export function enterGuestPreview(w: Window & typeof globalThis): void {
    const define = (name: string, value: unknown) => Object.defineProperty(w, name, { value, configurable: true, writable: false });
    define('localStorage', new MemoryStorage());
    define('sessionStorage', new MemoryStorage());
    define('indexedDB', undefined);
    define('caches', undefined);
    const send = w.fetch.bind(w);
    define('fetch', (input: RequestInfo | URL, init?: RequestInit) => send(new Request(input, { ...init, credentials: 'omit' })));
}

if (isGuestPreview) enterGuestPreview(window);
