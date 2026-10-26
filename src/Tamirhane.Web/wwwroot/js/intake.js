// Cihaz kabul: formdaki bilgiler yazıldıkça sağdaki iş emri etiketine geçer.
// Değerler textContent ile yazılır (HTML olarak değil); müşterinin yazdığı "<b>" etikette düz metin kalır.
(function () {
  var form = document.getElementById("intake-form");
  if (!form) return;

  function value(name) {
    var el = form.querySelector('[data-preview="' + name + '"]');
    return el ? el.value.trim() : "";
  }

  function show(name, text, empty) {
    var el = form.querySelector('[data-show="' + name + '"]');
    if (!el) return;
    el.textContent = text || empty;
    el.classList.toggle("is-empty", !text);
  }

  function update() {
    if (form.querySelector('[data-preview="customer"]')) show("customer", value("customer"), "adı yazılınca");

    // Kayıtlı bir cihaz seçildiyse etiket onu gösterir, yoksa yazılan marka ve model.
    var chosen = form.querySelector('input[name="Input.DeviceId"]:checked');
    var device = chosen && chosen.dataset.device ? chosen.dataset.device : (value("brand") + " " + value("model")).trim();
    show("device", device, "marka ve model");
    show("imei", chosen && chosen.dataset.device ? "" : value("imei"), "yok");
    show("complaint", value("complaint"), "şikâyet yazılınca");
    show("accessories", value("accessories"), "yok");
  }

  form.addEventListener("input", update);
  form.addEventListener("change", update);
  update();
})();
