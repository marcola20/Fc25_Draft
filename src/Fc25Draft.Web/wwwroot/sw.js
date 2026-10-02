// Service worker do app instalado (PWA). O site é Blazor Server e precisa da conexão o tempo todo,
// então nada é guardado para usar offline: só a página de "sem conexão", mostrada quando a rede cai
// ao abrir uma página. Os outros pedidos (scripts, API, SignalR) passam direto.
const CACHE = 'cbfv-offline-v1';
const OFFLINE = '/offline.html';

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE)
            .then(cache => cache.addAll([OFFLINE, '/icons/icon-192.png']))
            .then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(nomes => Promise.all(nomes.filter(n => n !== CACHE).map(n => caches.delete(n))))
            .then(() => self.clients.claim()));
});

self.addEventListener('fetch', event => {
    if (event.request.mode !== 'navigate') return;
    event.respondWith(fetch(event.request).catch(() => caches.match(OFFLINE)));
});
