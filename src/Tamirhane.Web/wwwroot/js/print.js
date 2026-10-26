// Yazdırma sayfaları: "Yazdır" düğmesi tarayıcının yazdırma penceresini açar (satır içi betik yok: CSP).
document.querySelectorAll("[data-print]").forEach(function (button) {
  button.addEventListener("click", function () { window.print(); });
});
