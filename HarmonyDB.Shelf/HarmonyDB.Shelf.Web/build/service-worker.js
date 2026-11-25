// The app's service worker (published as /service-worker.js with the manifest's version on its first line; see
// serviceWorker.ts). The old frontend's worker, copied: every file of the app is kept on the device, a new version is
// fetched whole and takes the open pages over; each page goes on as the version it started as and loads the new one at
// its next change of page (app/pwa.ts).
//
// A page is only ever given its own version's files. The worker remembers which version each page was started as (the
// app's page it was served; a worker the page starts is of the page's version too) and answers the page's files — a
// part of the app it loads later, its workers — from that version's kept files only, never from another version's, even
// under the same name. A version's files are kept while a page of it is open. A page started before any worker (its
// files came from the server) gets them from the server. A file of the page's version that is not there any more is
// refused: the page then loads the new version whole (app/pwa.ts), never a mix.
//
// Navigations get the app's page, except the server's own addresses — a sign-in comes back through /api/auth/callback/…,
// an MCP client's through /oauth/…, as navigations that must reach the server — and the build's other pages.

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
// only the app's own files and pages: everything else (the server's addresses, pictures from elsewhere) goes as the
// page asks it, untouched
self.addEventListener('fetch', event => {
    if (isApps(event.request)) event.respondWith(onFetch(event).catch(() => fetch(event.request)));
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

// which version each page (and each worker a page started) is: its client's id → its version's cache name
const pagesCacheName = 'offline-pages';
const pagePrefix = '/__page/';

// The record is kept in the device's storage, which can be full or fail: neither reading nor writing it may fail a
// page or its file. A page whose version could not be recorded or read is taken as one started without a worker: its
// files come from the server, and a part the server no longer has makes it load the new version whole (app/pwa.ts).

async function versionOf(clientId) {
    if (!clientId) return null;
    try {
        const kept = await (await caches.open(pagesCacheName)).match(pagePrefix + encodeURIComponent(clientId));
        return kept ? await kept.text() : null;
    } catch {
        return null;
    }
}

async function remember(clientId, version) {
    if (!clientId || !version) return;
    try {
        await (await caches.open(pagesCacheName)).put(pagePrefix + encodeURIComponent(clientId), new Response(version));
    } catch {
        // not recorded: the page is taken as one started without a worker
    }
}

async function onInstall() {
    // Activate the new worker as soon as the old one is retired.
    self.skipWaiting();

    const requests = self.assetsManifest.assets.map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(requests);
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
    await forget();
    // the open pages are this worker's now (each loads this version at its next change of page)
    await self.clients.claim();
}

/** What is remembered of closed pages goes, and so do the versions no open page is of. */
async function forget() {
    const open = new Set((await self.clients.matchAll({ includeUncontrolled: true, type: 'all' })).map(client => client.id));
    const pages = await caches.open(pagesCacheName);
    const used = new Set([cacheName]);
    for (const request of await pages.keys()) {
        const id = decodeURIComponent(new URL(request.url).pathname.slice(pagePrefix.length));
        if (open.has(id)) used.add(await (await pages.match(request)).text());
        else await pages.delete(request);
    }
    const keys = await caches.keys();
    await Promise.all(keys.filter(key => key.startsWith(cacheNamePrefix) && !used.has(key)).map(key => caches.delete(key)));
}

function isApps(request) {
    const url = new URL(request.url);
    return request.method === 'GET' && url.origin === self.origin
        && !serverPaths.some(pattern => pattern.test(url.pathname)) && !otherPages.some(pattern => pattern.test(url.pathname));
}

// (The device's storage failing to be read — or anything else going wrong here — leaves the request to the server, as
// for a page started without a worker.)
async function onFetch(event) {
    const request = event.request;
    // a new page of the app: this version's page, and the page is this version
    if (request.mode === 'navigate' && !manifestUrlList.includes(request.url)) {
        const page = await (await caches.open(cacheName)).match('index.html', { ignoreVary: true });
        if (!page) return fetch(request);
        await remember(event.resultingClientId, cacheName);
        return page;
    }

    // a file a page asks for: of the page's own version (a worker the page starts is of its version too)
    const version = await versionOf(event.clientId);
    await remember(event.resultingClientId, version);
    // a page started before any worker: its files are the server's
    if (version === null) return fetch(request);
    // its version is not kept any more: refused, and the page loads the new version whole
    if (!await caches.has(version)) return Response.error();
    const cache = await caches.open(version);
    // a file is the same whatever the request's headers (a module script's Origin): the server's Vary does not count
    const cached = await cache.match(request, { ignoreSearch: true, ignoreVary: true });
    if (cached) return cached;
    // a file kept the first time it is asked for (the same file in every version while its hash is the same)
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
    // what is not a file of the app (a file the app does not keep): the server's
    return fetch(request);
}
