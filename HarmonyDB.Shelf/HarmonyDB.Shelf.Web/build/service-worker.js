// The app's service worker (published as /service-worker.js with the manifest's version on its first line; see
// serviceWorker.ts). The old frontend's worker, copied: every file of the app is kept on the device, a new version is
// fetched whole and takes the open pages over; each page goes on as the version it started as and loads the new one at
// its next change of page (app/pwa.ts). So that it can, the version before is kept beside the new one (a page of it may
// still ask for its files: a part of the app loaded later, the player's workers); older ones go. Navigations get
// the app's page, except the server's own addresses — a sign-in comes back through /api/auth/callback/…, an MCP
// client's through /oauth/…, as navigations that must reach the server — and the build's other pages.

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
// only the app's own files and pages: everything else (the server's addresses, pictures from elsewhere) goes as the
// page asks it, untouched
self.addEventListener('fetch', event => {
    if (isApps(event.request)) event.respondWith(onFetch(event.request));
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const serverPaths = [/^\/api\//, /^\/mcp(\/|$)/, /^\/oauth\//, /^\/\.well-known\//];
// the build's pages that are not the app (the public page): never answered with the app's page
const otherPages = [/^\/public\.html$/];
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, self.origin).href);
// files too big to fetch for everyone at install (the fingerings' model): kept the first time they are asked for, and
// carried over to the next version while they are the same file
const onDemand = new Map(self.assetsManifest.onDemand.map(asset => [new URL(asset.url, self.origin).href, asset]));

// when a version's cache was made (an entry of its own), to tell the version before from older ones
const madeKey = '/__made';

async function onInstall() {
    // Activate the new worker as soon as the old one is retired.
    self.skipWaiting();

    const requests = self.assetsManifest.assets.map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(requests);
    await cache.put(madeKey, new Response(String(Date.now())));
}

async function madeAt(key) {
    const made = await (await caches.open(key)).match(madeKey);
    return made ? Number(await made.text()) : 0;
}

async function onActivate() {
    const keys = await caches.keys();
    const cache = await caches.open(cacheName);
    for (const url of onDemand.keys()) {
        if (await cache.match(url)) continue;
        for (const key of keys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)) {
            const kept = await (await caches.open(key)).match(url);
            if (kept) {
                await cache.put(url, kept);
                break;
            }
        }
    }
    // the version before stays (its pages may still ask for their files); older ones go
    const others = keys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName);
    const made = await Promise.all(others.map(async key => ({ key, at: await madeAt(key) })));
    made.sort((a, b) => b.at - a.at);
    await Promise.all(made.slice(1).map(({ key }) => caches.delete(key)));
    // the open pages are this worker's now (each loads this version at its next change of page)
    await self.clients.claim();
}

function isApps(request) {
    const url = new URL(request.url);
    return request.method === 'GET' && url.origin === self.origin
        && !serverPaths.some(pattern => pattern.test(url.pathname)) && !otherPages.some(pattern => pattern.test(url.pathname));
}

async function onFetch(request) {
    const appPage = request.mode === 'navigate' && !manifestUrlList.includes(request.url);
    const cache = await caches.open(cacheName);
    const cached = await cache.match(appPage ? 'index.html' : request, { ignoreSearch: true });
    if (cached) return cached;
    // a file of the version before, for a page of it still open
    const before = appPage ? undefined : await caches.match(request, { ignoreSearch: true });
    if (before) return before;
    const later = onDemand.get(request.url);
    if (!later) return fetch(request);
    const response = await fetch(new Request(later.url, { integrity: later.hash }));
    if (response.ok) await cache.put(request, response.clone());
    return response;
}
