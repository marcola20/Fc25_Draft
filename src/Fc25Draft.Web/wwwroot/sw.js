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

// Notificações no celular (Web Push): o servidor manda { title, body, url, tag }.
self.addEventListener('push', event => {
    let dados = {};
    try { dados = event.data ? event.data.json() : {}; } catch (e) { dados = { body: event.data ? event.data.text() : '' }; }
    event.waitUntil(self.registration.showNotification(dados.title || 'CBFV', {
        body: dados.body || '',
        icon: '/icons/icon-192.png',
        badge: '/icons/badge-96.png',
        tag: dados.tag,
        data: { url: dados.url || '/minha-area' }
    }));
});

// Tocar na notificação abre (ou traz para frente) o site na página do aviso.
self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin).href;
    event.waitUntil((async () => {
        const abertas = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const janela of abertas) {
            if (janela.url.startsWith(self.location.origin) && 'focus' in janela) {
                await janela.focus();
                if ('navigate' in janela) await janela.navigate(url);
                return;
            }
        }
        await self.clients.openWindow(url);
    })());
});
