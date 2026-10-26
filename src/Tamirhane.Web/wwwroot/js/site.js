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
  // Düğmede sadece ikon var; ne yapacağı ekran okuyucuya ve fare ipucuna yazılır.
  var label = function () {
    var text = isDark() ? "Açık tema" : "Koyu tema";
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
