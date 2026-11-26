// The app's service worker (published as /service-worker.js with the manifest's version on its first line; see
// serviceWorker.ts): every file of the current build is kept on the device, so the app opens without a connection. A
// new build is fetched whole, replaces the one kept, and takes the open pages over. The built files are named by their
// content, so a name never means two contents; a page of the build before that asks for a part no longer there gets
// the server's "not there", and loads the new build (app/pwa.ts).
//
// Navigations get the app's page, except the server's own addresses — a sign-in comes back through /api/auth/callback/…,
// an MCP client's through /oauth/…, as navigations that must reach the server.

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
// only the app's own files and pages: everything else (the server's addresses, pictures from elsewhere) goes as the
// page asks it, untouched
self.addEventListener('fetch', event => {
    if (isApps(event.request)) event.respondWith(onFetch(event.request).catch(() => fetch(event.request)));
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const serverPaths = [/^\/api\//, /^\/mcp(\/|$)/, /^\/oauth\//, /^\/\.well-known\//];
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, self.origin).href);
// files too big to fetch for everyone at install (the fingerings' model): kept the first time they are asked for, and
// carried over to the next build while they are the same file
const onDemand = new Map(self.assetsManifest.onDemand.map(asset => [new URL(asset.url, self.origin).href, asset]));

async function onInstall() {
    // Activate the new worker as soon as the old one is retired.
    self.skipWaiting();
    const requests = self.assetsManifest.assets.map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(requests);
}

async function onActivate() {
    const keys = (await caches.keys()).filter(key => key !== cacheName);
    const cache = await caches.open(cacheName);
    for (const url of onDemand.keys()) {
        if (await cache.match(url)) continue;
        for (const key of keys.filter(key => key.startsWith(cacheNamePrefix))) {
            const kept = await (await caches.open(key)).match(url);
            if (kept) {
                await cache.put(url, kept);
                break;
            }
        }
    }
    // only the current build is kept
    await Promise.all(keys.map(key => caches.delete(key)));
    await self.clients.claim();
}

function isApps(request) {
    const url = new URL(request.url);
    return request.method === 'GET' && url.origin === self.origin && !serverPaths.some(pattern => pattern.test(url.pathname));
}

async function onFetch(request) {
    const cache = await caches.open(cacheName);
    // a page of the app: the app's page
    if (request.mode === 'navigate' && !manifestUrlList.includes(request.url)) {
        return (await cache.match('index.html', { ignoreVary: true })) ?? fetch(request);
    }
    // a file is the same whatever the request's headers (a module script's Origin): the server's Vary does not count
    const cached = await cache.match(request, { ignoreSearch: true, ignoreVary: true });
    if (cached) return cached;
    // a file kept the first time it is asked for
    const later = onDemand.get(request.url);
    if (later) {
        const response = await fetch(new Request(later.url, { integrity: later.hash }));
        if (response.ok) {
            try {
                await cache.put(request, response.clone());
            } catch {
                // not kept (the storage is full): given all the same, and asked again next time
            }
        }
        return response;
    }
    // not a kept file of this build: the server's
    return fetch(request);
}
