// Takip sayfası: karar formu bir kez gönderilir. Düğmeler gönderimden hemen sonra kilitlenir; önce kilitlense
// tıklanan düğmenin değeri (Onayla / İstemiyorum) forma eklenmezdi. Asıl koruma sunucuda (karar bir kez kaydedilir).
document.querySelectorAll("form[data-once]").forEach(function (form) {
  form.addEventListener("submit", function () {
    setTimeout(function () {
      form.querySelectorAll("button").forEach(function (b) { b.disabled = true; });
    }, 0);
  });
});

// Tarayıcı bildirimi: "Durum değişince haber ver". Tarayıcı desteklemiyorsa (ör. iPhone'da Ana Ekran'a eklenmemiş
// Safari) bölüm gizli kalır. Aynı tarayıcı birden çok iş emrine abone olabilir; bu yüzden "durdur" tarayıcının
// aboneliğini silmez, sadece bu iş emrinden çıkarır. Asıl kurallar (hangi adresler kabul edilir) sunucuda.
(function () {
  var box = document.querySelector("[data-push]");
  if (!box || !("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) return;

  var scope = "/t/";
  var token = box.querySelector("input[name='__RequestVerificationToken']").value;
  var on = box.querySelector("[data-push-on]");
  var off = box.querySelector("[data-push-off]");
  var text = box.querySelector("[data-push-text]");
  var message = box.querySelector("[data-push-message]");
  var idleText = text.textContent;

  function post(url, body) {
    return fetch(url, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
      body: JSON.stringify(body),
    });
  }

  function show(subscribed) {
    on.hidden = subscribed;
    off.hidden = !subscribed;
    text.textContent = subscribed ? box.dataset.msgActive : idleText;
    box.hidden = false;
  }

  function say(value) { message.textContent = value; }

  // VAPID açık anahtarı base64url; tarayıcı bayt dizisi ister.
  function bytes(base64url) {
    var padded = base64url.replace(/-/g, "+").replace(/_/g, "/") + "===".slice((base64url.length + 3) % 4);
    var raw = atob(padded);
    var result = new Uint8Array(raw.length);
    for (var i = 0; i < raw.length; i++) result[i] = raw.charCodeAt(i);
    return result;
  }

  function sameKey(subscription, key) {
    var current = subscription.options && subscription.options.applicationServerKey;
    if (!current) return true; // bazı tarayıcılar vermez; sunucu yine doğru anahtarla gönderir
    var a = new Uint8Array(current);
    if (a.length !== key.length) return false;
    for (var i = 0; i < a.length; i++) if (a[i] !== key[i]) return false;
    return true;
  }

  function existing() {
    return navigator.serviceWorker.getRegistration(scope).then(function (registration) {
      return registration ? registration.pushManager.getSubscription() : null;
    });
  }

  existing()
    .then(function (subscription) {
      if (!subscription) return false;
      return post(box.dataset.state, subscription.toJSON())
        .then(function (response) { return response.ok ? response.json() : { subscribed: false }; })
        .then(function (state) { return state.subscribed === true; });
    })
    .then(show, function () { show(false); });

  on.addEventListener("click", function () {
    var key = bytes(box.dataset.key);
    on.disabled = true;
    say("");
    Notification.requestPermission()
      .then(function (permission) {
        if (permission !== "granted") throw new Error("permission");
        return navigator.serviceWorker.register("/sw.js", { scope: scope });
      })
      .then(function () { return navigator.serviceWorker.ready; })
      .then(function (registration) {
        return registration.pushManager.getSubscription().then(function (subscription) {
          // Sunucunun anahtarı değiştiyse eski abonelik çalışmaz: bırakıp yenisini al.
          if (subscription && sameKey(subscription, key)) return subscription;
          var renew = subscription ? subscription.unsubscribe() : Promise.resolve();
          return renew.then(function () {
            return registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
          });
        });
      })
      .then(function (subscription) { return post(box.dataset.subscribe, subscription.toJSON()); })
      .then(function (response) {
        if (!response.ok) throw new Error("server");
        show(true);
        say(box.dataset.msgOn);
      })
      .catch(function (error) {
        say(error.message === "permission"
          ? box.dataset.msgDenied
          : box.dataset.msgOnFailed);
      })
      .finally(function () { on.disabled = false; });
  });

  off.addEventListener("click", function () {
    off.disabled = true;
    say("");
    existing()
      .then(function (subscription) {
        if (!subscription) return { ok: true };
        return post(box.dataset.unsubscribe, { endpoint: subscription.endpoint });
      })
      .then(function (response) {
        if (!response.ok) throw new Error("server");
        show(false);
        say(box.dataset.msgOff);
      })
      .catch(function () { say(box.dataset.msgOffFailed); })
      .finally(function () { off.disabled = false; });
  });
})();
