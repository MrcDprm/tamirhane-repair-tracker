// Tezgâh kısayolları, tema ve dar ekran menüsü. Satır içi betik yok (CSP); her şey bu dosyada.

// "/" aramaya odaklanır, "N" yeni cihaz kabulünü açar. Bir alana yazarken ya da Ctrl/Alt/Cmd ile basılınca çalışmaz.
document.addEventListener("keydown", function (event) {
  if (event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented) return;
  var target = event.target;
  if (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName)) return;

  if (event.key === "/") {
    var search = document.getElementById("search");
    if (search) {
      event.preventDefault();
      search.focus();
      search.select();
    }
  } else if (event.key === "n" || event.key === "N") {
    var intake = document.getElementById("intake-link");
    if (intake) {
      event.preventDefault();
      intake.click();
    }
  }
});

// Geri alınamayan işlemler (personel çıkarma): formun data-confirm metni sorulur.
document.addEventListener("submit", function (event) {
  var message = event.target.dataset && event.target.dataset.confirm;
  if (message && !confirm(message)) event.preventDefault();
});

// Gönderilen form: basılan düğme hemen "işleniyor" olur ve form ikinci kez gönderilmez (yavaş bağlantıda tekrar
// tıklayıp aynı kaydı iki kez açmak olmasın). Düğme bir sonraki adımda kilitlenir: hemen kilitlense tıklanan düğmenin
// değeri (ör. hangi adım) forma eklenmezdi. Geri tuşuyla dönülen sayfada (önbellekten) kilit kalkar.
function busy(button, on) {
  if (!button) return;
  button.classList.toggle("is-busy", on);
  button.disabled = on;
  if (on) button.setAttribute("aria-busy", "true"); else button.removeAttribute("aria-busy");
}
document.addEventListener("submit", function (event) {
  var form = event.target;
  if (form.target === "_blank") return;
  if (form.dataset.submitting) { event.preventDefault(); return; }
  if (event.defaultPrevented) return; // onay sorusu reddedildi ya da htmx kendisi gönderiyor (aşağıda)
  form.dataset.submitting = "1";
  var button = event.submitter || form.querySelector("button[type=submit], button:not([type])");
  setTimeout(function () { busy(button, true); }, 0);
});
window.addEventListener("pageshow", function () {
  document.querySelectorAll("form[data-submitting]").forEach(function (form) {
    delete form.dataset.submitting;
    form.querySelectorAll(".is-busy").forEach(function (b) { busy(b, false); });
  });
});

// htmx ile giden formlar (iş emri sayfası): istek sürerken düğme işleniyor; cevap gelince (hata olsa da) açılır.
document.addEventListener("htmx:beforeRequest", function (event) {
  var form = event.detail.elt.closest("form");
  if (form) form.querySelectorAll("button").forEach(function (b) { busy(b, true); });
});
document.addEventListener("htmx:afterRequest", function (event) {
  var form = event.detail.elt.closest("form");
  if (form && form.isConnected) form.querySelectorAll("button").forEach(function (b) { busy(b, false); });
});

// Dar ekranda yan menü (pano) katlanır; düğme açar ve kapatır.
var sideToggle = document.querySelector(".side-toggle");
if (sideToggle) {
  sideToggle.addEventListener("click", function () {
    var side = sideToggle.closest(".side");
    var open = side.classList.toggle("is-open");
    sideToggle.setAttribute("aria-expanded", String(open));
  });
}

// Tema: açık ↔ koyu; seçim bu cihazda hatırlanır.
var themeButton = document.getElementById("theme-toggle");
if (themeButton) {
  var isDark = function () {
    return document.documentElement.dataset.theme === "dark" ||
      (!document.documentElement.dataset.theme && matchMedia("(prefers-color-scheme: dark)").matches);
  };
  // Düğmede sadece ikon var; ne yapacağı ekran okuyucuya ve fare ipucuna yazılır (metinler sayfanın dilinde, data-*'dan).
  var label = function () {
    var text = isDark() ? themeButton.dataset.light : themeButton.dataset.dark;
    themeButton.setAttribute("aria-label", text);
    themeButton.title = text;
  };
  label();
  themeButton.addEventListener("click", function () {
    var next = isDark() ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    try { localStorage.setItem("tamirhane-theme", next); } catch (e) { /* depolama kapalı: sadece bu sayfa */ }
    label();
  });
}

// Alt bilgideki yıl: sunucunun yazdığı yıl, ziyaretçinin saatindeki yılla güncellenir (sayfa önbellekten gelse de).
document.querySelectorAll("[data-year]").forEach(function (el) {
  el.textContent = String(new Date().getFullYear());
});
