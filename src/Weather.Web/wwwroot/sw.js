// Service worker.
//
// Blazor Server needs a live circuit, so the interactive page genuinely cannot run offline. What can run
// offline is a static snapshot: the worker keeps the last successful /api/weather response and serves a
// small standalone page that renders it when the network is gone. That is an honest offline story rather
// than an app shell that loads and then sits there unable to do anything.

const VERSION = 'weather-v2';
const SHELL = `${VERSION}-shell`;
const DATA = `${VERSION}-data`;

const PRECACHE = [
    '/offline.html',
    '/app.css',
    '/favicon.svg',
    '/manifest.webmanifest'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(SHELL)
            .then(cache => cache.addAll(PRECACHE))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys
                .filter(key => !key.startsWith(VERSION))
                .map(key => caches.delete(key))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const request = event.request;

    if (request.method !== 'GET' || new URL(request.url).origin !== self.location.origin) {
        return;
    }

    if (new URL(request.url).pathname === '/api/weather') {
        event.respondWith(networkFirstData(request));
        return;
    }

    if (request.mode === 'navigate') {
        event.respondWith(navigateOrOffline(request));
        return;
    }

    if (isStaticAsset(request)) {
        event.respondWith(staleWhileRevalidate(request));
    }
});

async function networkFirstData(request) {
    try {
        const response = await fetch(request);

        if (response.ok) {
            const cache = await caches.open(DATA);
            await cache.put(request, response.clone());
        }

        return response;
    } catch {
        const cached = await caches.match(request);
        return cached ?? Response.error();
    }
}

async function navigateOrOffline(request) {
    try {
        return await fetch(request);
    } catch {
        const offline = await caches.match('/offline.html');
        return offline ?? Response.error();
    }
}

// Stale-while-revalidate, not cache-first.
//
// These assets are served without content hashes in their names, so cache-first would pin a visitor to
// the CSS and JS of whatever deploy they first saw — forever, because the cache version is a constant.
// Serving the cached copy and refreshing it in the background costs at most one stale load after a deploy.
async function staleWhileRevalidate(request) {
    const cache = await caches.open(SHELL);
    const cached = await cache.match(request);

    const network = fetch(request)
        .then(response => {
            if (response.ok) {
                cache.put(request, response.clone());
            }

            return response;
        })
        .catch(() => null);

    if (cached) {
        return cached;
    }

    const response = await network;

    return response ?? Response.error();
}

function isStaticAsset(request) {
    const path = new URL(request.url).pathname;

    // The Blazor framework files are deliberately excluded: caching them risks serving a client that no
    // longer matches the server it has to talk to.
    return path.startsWith('/js/')
        || path.startsWith('/lib/')
        || path.startsWith('/icons/')
        || path === '/app.css'
        || path === '/favicon.svg';
}
