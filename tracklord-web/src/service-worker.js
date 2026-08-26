/// <reference types="@sveltejs/kit" />
import { build, files, version } from '$service-worker';

// Create a unique cache name for this version
const CACHE_NAME = `tracklord-cache-${version}`;

// Filter assets to cache (exclude robots.txt or manifest duplicates if any)
const ASSETS = [
  ...build,
  ...files
];

self.addEventListener('install', (event) => {
  // Pre-cache all static files and JavaScript chunks
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(ASSETS))
      .then(() => {
        // Activate immediately without waiting for other tabs to close
        return self.skipWaiting();
      })
  );
});

self.addEventListener('activate', (event) => {
  // Remove stale caches from previous versions
  event.waitUntil(
    caches.keys().then((keys) => {
      return Promise.all(
        keys.map((key) => {
          if (key !== CACHE_NAME) {
            return caches.delete(key);
          }
        })
      );
    }).then(() => {
      // Claim clients immediately so the service worker controls the page on first reload
      return self.clients.claim();
    })
  );
});

self.addEventListener('fetch', (event) => {
  // Only handle GET requests
  if (event.request.method !== 'GET') return;

  const url = new URL(event.request.url);

  // 1. Never cache Firebase Auth / reCAPTCHA or dynamic backend API endpoints
  if (
    url.pathname.startsWith('/api/') ||
    url.hostname.includes('identitytoolkit.googleapis.com') ||
    url.hostname.includes('securetoken.googleapis.com') ||
    url.hostname.includes('recaptcha') ||
    url.hostname.includes('firebaseapp.com')
  ) {
    // Attempt network fetch, provide clean fallback if totally offline
    event.respondWith(
      fetch(event.request).catch(() => {
        return new Response(
          JSON.stringify({
            error: 'Offline',
            message: 'You are currently offline. Please reconnect to perform this action.'
          }),
          {
            status: 503,
            statusText: 'Service Unavailable (Offline)',
            headers: { 'Content-Type': 'application/json' }
          }
        );
      })
    );
    return;
  }

  // 2. Static application bundle assets (immutable build files & static assets)
  const isStaticAsset = url.origin === self.origin && (
    build.includes(url.pathname) || files.includes(url.pathname)
  );

  if (isStaticAsset) {
    event.respondWith(
      caches.open(CACHE_NAME).then(async (cache) => {
        const cached = await cache.match(event.request);
        if (cached) return cached;

        try {
          const response = await fetch(event.request);
          if (response && response.status === 200) {
            cache.put(event.request, response.clone());
          }
          return response;
        } catch (err) {
          return cached || new Response('Offline asset unavailable', { status: 404 });
        }
      })
    );
    return;
  }

  // 3. Navigation requests (Page routes) -> Network First with App Shell Cache Fallback
  if (event.request.mode === 'navigate' || event.request.headers.get('accept')?.includes('text/html')) {
    event.respondWith(
      fetch(event.request)
        .then((response) => {
          if (response && response.status === 200) {
            const responseClone = response.clone();
            caches.open(CACHE_NAME).then((cache) => {
              cache.put(event.request, responseClone);
            });
          }
          return response;
        })
        .catch(async () => {
          // If network is offline, retrieve matching cached page or fallback to root shell
          const cached = await caches.match(event.request);
          if (cached) return cached;

          // Try root shell index fallback
          const fallback = await caches.match('/');
          if (fallback) return fallback;

          return new Response(
            `<!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8" />
                <title>Offline - TrackLord</title>
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <style>
                  body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; background: #f8f9ff; color: #0b1c30; text-align: center; }
                  .card { background: white; padding: 2.5rem; border-radius: 1rem; box-shadow: 0 10px 25px rgba(0,74,198,0.1); max-width: 420px; }
                  h1 { color: #004ac6; margin-top: 0; }
                  p { color: #434655; line-height: 1.5; }
                  button { background: #004ac6; color: white; border: none; padding: 0.75rem 1.5rem; border-radius: 0.5rem; font-weight: 600; cursor: pointer; margin-top: 1rem; }
                </style>
              </head>
              <body>
                <div class="card">
                  <h1>You're Offline</h1>
                  <p>TrackLord is currently operating offline. Reconnect to sync latest lots, materials, and batch movements.</p>
                  <button onclick="window.location.reload()">Retry Connection</button>
                </div>
              </body>
            </html>`,
            { headers: { 'Content-Type': 'text/html' } }
          );
        })
    );
    return;
  }

  // 4. External resources (Google Fonts, CDNs) -> Stale While Revalidate
  event.respondWith(
    caches.open(CACHE_NAME).then(async (cache) => {
      const cached = await cache.match(event.request);
      const networkFetch = fetch(event.request)
        .then((response) => {
          if (response && response.status === 200) {
            cache.put(event.request, response.clone());
          }
          return response;
        })
        .catch(() => cached);

      return cached || networkFetch;
    })
  );
});

// Support manual cache updating from clients
self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});
