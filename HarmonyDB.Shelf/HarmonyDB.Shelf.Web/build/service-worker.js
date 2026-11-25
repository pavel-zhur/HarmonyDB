// The app's service worker (published as /service-worker.js with the manifest's version on its first line; see
// serviceWorker.ts). The old frontend's worker, copied: every file of the app is kept on the device, a new version is
// fetched whole and replaces the old one (the open page is told, and offers to reload: app/pwa.ts). Navigations get
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

async function onInstall() {
    // Activate the new worker as soon as the old one is retired.
    self.skipWaiting();

    const requests = self.assetsManifest.assets.map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(requests));
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
    await Promise.all(keys.filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName).map(key => caches.delete(key)));
    // the open pages switch to this version (they offer to reload)
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
    const later = onDemand.get(request.url);
    if (!later) return fetch(request);
    const response = await fetch(new Request(later.url, { integrity: later.hash }));
    if (response.ok) await cache.put(request, response.clone());
    return response;
}
