// The installed app: its worker (a build only; build/service-worker.js keeps the current build on the device). After a
// new build the open page goes on as it is; a part of it that it loads later and that is not there any more (the files
// of a build are named by their content, and the old ones are gone) makes the page load the new build — once: a page
// that still misses it right after that stays as it is.

const reloadedKey = 'shelf.reloadedForMissingPart';

export function installWorker(): void {
    if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return;

    addEventListener('vite:preloadError', event => {
        try {
            const last = Number(sessionStorage.getItem(reloadedKey) ?? 0);
            if (Date.now() - last < 60_000) return;
            sessionStorage.setItem(reloadedKey, String(Date.now()));
        } catch {
            return;
        }
        event.preventDefault();
        location.reload();
    });

    void navigator.serviceWorker.register('/service-worker.js').then(registration => {
        // a page kept open for days looks for a new build now and then
        setInterval(() => void registration.update(), 60 * 60 * 1000);
    });
}
