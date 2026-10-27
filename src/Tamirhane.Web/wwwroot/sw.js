// Tarayıcı bildirimi (Web Push) için service worker. Sadece /t/ (takip sayfaları) kapsamında kayıtlıdır.
// Sunucu iş emrinin durumu değişince şifreli bir mesaj gönderir; tarayıcı onu çözüp buraya { title, body, url } verir.

self.addEventListener("push", function (event) {
  var data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch (e) {
    data = {};
  }
  event.waitUntil(self.registration.showNotification(data.title || "Tamirhane", {
    body: data.body || "",
    icon: "/img/notification-192.png",
    // Aynı iş emrinin yeni bildirimi eskisinin yerine geçer; bildirim merkezinde üst üste birikmez.
    tag: data.url || "tamirhane",
    renotify: true,
    data: { url: data.url },
  }));
});

self.addEventListener("notificationclick", function (event) {
  event.notification.close();
  var target = trackingUrl(event.notification.data && event.notification.data.url);
  if (!target) return;
  event.waitUntil(self.clients.matchAll({ type: "window", includeUncontrolled: true }).then(function (windows) {
    for (var i = 0; i < windows.length; i++) {
      if (windows[i].url === target && "focus" in windows[i]) return windows[i].focus();
    }
    return self.clients.openWindow(target);
  }));
});

// Bildirimdeki link sadece bu sitenin bir takip sayfasıysa açılır; başka bir adrese yönlendirme yapılmaz.
function trackingUrl(url) {
  try {
    var parsed = new URL(url);
    return parsed.origin === self.location.origin && parsed.pathname.indexOf("/t/") === 0 ? parsed.href : null;
  } catch (e) {
    return null;
  }
}
