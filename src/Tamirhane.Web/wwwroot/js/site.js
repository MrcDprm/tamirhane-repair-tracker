// Tezgâh kısayolları ve tema düğmesi. Satır içi betik yok (CSP); her şey bu dosyada.

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

// Tema: açık ↔ koyu; seçim bu cihazda hatırlanır.
var themeButton = document.getElementById("theme-toggle");
if (themeButton) {
  var label = function () {
    var dark = document.documentElement.dataset.theme === "dark" ||
      (!document.documentElement.dataset.theme && matchMedia("(prefers-color-scheme: dark)").matches);
    themeButton.textContent = dark ? "Açık temaya geç" : "Koyu temaya geç";
    return dark;
  };
  label();
  themeButton.addEventListener("click", function () {
    var next = label() ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    try { localStorage.setItem("tamirhane-theme", next); } catch (e) { /* depolama kapalı: sadece bu sayfa */ }
    label();
  });
}
