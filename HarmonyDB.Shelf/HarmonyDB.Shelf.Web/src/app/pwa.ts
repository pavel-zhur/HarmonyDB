// The installed app: its worker (a build only), and a new version taken without anyone having to know about reloads.
//
// The worker installs a new version in the background and takes the open page over (build/service-worker.js), keeping
// the version before beside it. The page goes on as the version it started as — whole, never half old and half new —
// and loads the new one at its next change of page (another path; a search typed into the address is not one), when
// nothing would be lost by it:
// - the connection is there (what the page shows now is in its memory; offline, a reload would show less);
// - no music is playing.
// Otherwise it waits for the next change of page. A part of the old version that is not there any more makes the page
// load the new version at once — once.

const reloadedKey = 'shelf.reloadedForMissingPart';

export function installWorker(): void {
    if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return;
    let controlled = !!navigator.serviceWorker.controller;
    let newVersion = false;
    let deciding = false;

    navigator.serviceWorker.addEventListener('controllerchange', () => {
        // the first install takes a page without a worker over: it is that version; any later one is newer
        if (controlled) newVersion = true;
        controlled = true;
    });

    const takeNewVersion = async () => {
        if (deciding) return;
        deciding = true;
        try {
            if (!navigator.onLine) return;
            const { player } = await import('../player/controller');
            if (player.getState().playing) return;
            // the connection itself, not the device's word for it
            const answer = await fetch('/api/app/health', { cache: 'no-store' }).catch(() => null);
            if (answer?.ok) location.reload();
        } finally {
            deciding = false;
        }
    };

    // a change of page: the new version, when it is there and nothing is lost by it
    void import('./routes').then(({ router }) => {
        let path = router.state.location.pathname;
        router.subscribe(state => {
            if (state.location.pathname === path) return;
            path = state.location.pathname;
            if (newVersion) void takeNewVersion();
        });
    });

    // a part of the app that the old version wants and the new one has not: the new version, here — once (a page
    // that still misses it after that stays as it is)
    addEventListener('vite:preloadError', event => {
        try {
            const last = JSON.parse(sessionStorage.getItem(reloadedKey) ?? 'null') as { href: string; at: number } | null;
            if (last && last.href === location.href && Date.now() - last.at < 60_000) return;
            sessionStorage.setItem(reloadedKey, JSON.stringify({ href: location.href, at: Date.now() }));
        } catch {
            return;
        }
        event.preventDefault();
        location.reload();
    });

    void navigator.serviceWorker.register('/service-worker.js').then(registration => {
        // a page kept open for days looks for a new version now and then
        setInterval(() => void registration.update(), 60 * 60 * 1000);
    });
}
