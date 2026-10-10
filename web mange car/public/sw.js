const CACHE_NAME = 'taj-car-rental-v3';
const STATIC_ASSETS = [
  './',
  './index.html',
  './manifest.json',
  './images/logo_512.png',
  './images/logo_192.png',
  './images/logo_maskable.png',
  './images/apple-touch-icon.png',
  './images/favicon-32x32.png',
  './images/logo.png'
];

// التثبيت وتخزين الملفات الأساسية
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(STATIC_ASSETS).catch((err) => {
        console.warn('SW: Some static assets failed to precache:', err);
      });
    }).then(() => self.skipWaiting())
  );
});

// تفعيل السيرفيس وركر وتحديث الكاش القديم
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames.map((cacheName) => {
          if (cacheName !== CACHE_NAME) {
            return caches.delete(cacheName);
          }
        })
      );
    }).then(() => self.clients.claim())
  );
});

// استراتيجية جلب البيانات: Network First مع Fallback إلى Cache
self.addEventListener('fetch', (event) => {
  // تجاهل طلبات غير GET أو طلبات chrome-extension أو طلبات Supabase API المباشرة
  if (event.request.method !== 'GET') return;
  const url = new URL(event.request.url);

  // إذا كان طلب Supabase API أو خارجي، نعتمد على الشبكة أولاً
  if (url.origin.includes('supabase.co')) {
    return;
  }

  // للأصول الثابتة والصور: Cache First مع تحديث بالخلفية
  if (url.pathname.match(/\.(png|jpg|jpeg|svg|gif|webp|woff2|woff|ttf|css|js)$/)) {
    event.respondWith(
      caches.match(event.request).then((cachedResponse) => {
        if (cachedResponse) {
          // تحديث بالخلفية
          fetch(event.request).then((networkResponse) => {
            if (networkResponse && networkResponse.status === 200) {
              caches.open(CACHE_NAME).then((cache) => cache.put(event.request, networkResponse));
            }
          }).catch(() => {});
          return cachedResponse;
        }

        return fetch(event.request).then((networkResponse) => {
          if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
            return networkResponse;
          }
          const responseToCache = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseToCache));
          return networkResponse;
        });
      })
    );
    return;
  }

  // لصفحات HTML والملاحة: Network First مع العودة إلى الكاش عند انقطاع الإنترنت
  event.respondWith(
    fetch(event.request)
      .then((response) => {
        if (response && response.status === 200) {
          const responseToCache = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseToCache));
        }
        return response;
      })
      .catch(() => {
        return caches.match(event.request).then((cachedResponse) => {
          if (cachedResponse) return cachedResponse;
          if (event.request.headers.get('accept')?.includes('text/html')) {
            return caches.match('./index.html');
          }
        });
      })
  );
});
