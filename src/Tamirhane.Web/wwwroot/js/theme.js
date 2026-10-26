// Seçilen tema sayfa çizilmeden uygulanır (beyaz parlama olmasın); <head> içinde, defer'siz yüklenir.
// Seçim yoksa işletim sisteminin tercihi (prefers-color-scheme) geçerli.
// "js" sınıfı: JavaScript çalışıyorsa dar ekranda menü katlanır; çalışmıyorsa açık kalır.
document.documentElement.classList.add("js");
try {
  var theme = localStorage.getItem("tamirhane-theme");
  if (theme === "light" || theme === "dark") document.documentElement.dataset.theme = theme;
} catch (e) { /* gizli pencerede depolama kapalı olabilir; tercih yine işletim sisteminden */ }
