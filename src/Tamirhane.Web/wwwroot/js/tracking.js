// Takip sayfası: karar formu bir kez gönderilir. Düğmeler gönderimden hemen sonra kilitlenir; önce kilitlense
// tıklanan düğmenin değeri (Onayla / İstemiyorum) forma eklenmezdi. Asıl koruma sunucuda (karar bir kez kaydedilir).
document.querySelectorAll("form[data-once]").forEach(function (form) {
  form.addEventListener("submit", function () {
    setTimeout(function () {
      form.querySelectorAll("button").forEach(function (b) { b.disabled = true; });
    }, 0);
  });
});
