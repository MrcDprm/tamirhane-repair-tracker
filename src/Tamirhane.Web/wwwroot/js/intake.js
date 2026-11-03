// Cihaz kabul: tür seçimine göre form değişir (marka listesi, IMEI, ekran kilidi, aksesuar ve durum seçenekleri) ve
// formdaki bilgiler yazıldıkça sağdaki iş emri etiketine geçer.
// Değerler textContent ile yazılır (HTML olarak değil); müşterinin yazdığı "<b>" etikette düz metin kalır.
(function () {
  var form = document.getElementById("intake-form");
  if (!form) return;

  var kindSelect = form.querySelector("[data-kind-select]");
  var brandPick = form.querySelector("[data-brand-pick]");
  var brandInput = form.querySelector("[data-brand-input]");
  var brandLabel = form.querySelector("[data-brand-label]");
  var modelInput = form.querySelector('[data-preview="model"]');
  var modelList = document.getElementById("model-options");
  var OTHER = "__other";

  // --- Türe göre alanlar ----------------------------------------------------------------------------------------
  function applyKind(kind) {
    form.querySelectorAll("[data-kinds]").forEach(function (el) {
      el.hidden = el.dataset.kinds.split(" ").indexOf(kind) < 0;
    });
    buildBrands(kind);
  }

  // Marka: türün listesi + "Diğer (yaz)". Listede olmayan marka yazılmışsa (ör. hata sonrası) "Diğer" seçili gelir.
  function buildBrands(kind) {
    if (!brandPick || !brandInput) return;
    var template = document.getElementById("brands-" + kind);
    var brands = template ? Array.prototype.map.call(template.content.querySelectorAll("option"), function (o) { return o.value; }) : [];
    brandPick.textContent = "";
    if (brands.length === 0) {
      // Bu türün hazır markası yok: sadece yazma alanı.
      brandPick.hidden = true;
      brandInput.hidden = false;
      brandLabel.htmlFor = brandInput.id;
      return;
    }
    addOption(brandPick, "", brandPick.dataset.choose);
    brands.forEach(function (b) { addOption(brandPick, b, b); });
    addOption(brandPick, OTHER, brandPick.dataset.other);

    var typed = brandInput.value.trim();
    var match = brands.filter(function (b) { return b.toLowerCase() === typed.toLowerCase(); })[0];
    brandPick.value = match || (typed ? OTHER : "");
    brandPick.hidden = false;
    brandLabel.htmlFor = brandPick.id;
    syncBrand();
  }

  function addOption(select, value, text) {
    var option = document.createElement("option");
    option.value = value;
    option.textContent = text;
    select.appendChild(option);
  }

  // Listeden seçilen marka yazma alanına geçer (sunucuya giden o); "Diğer" seçilince alan açılır.
  function syncBrand() {
    var other = brandPick.value === OTHER;
    brandInput.hidden = !other;
    if (!other) brandInput.value = brandPick.value;
    loadModels();
  }

  // --- Model önerileri: atölyenin geçmişi + hazır liste (sunucudan, tür ve marka başına bir kez) --------------------
  var cache = {};
  function loadModels() {
    if (!modelInput || !modelList) return;
    var kind = kindSelect.value;
    var brand = brandInput.value.trim();
    var key = kind + "|" + brand.toLowerCase();
    modelList.textContent = "";
    if (!brand) return;
    var fill = function (models) {
      if (kindSelect.value + "|" + brandInput.value.trim().toLowerCase() !== key) return; // bu arada değişti
      modelList.textContent = "";
      models.forEach(function (m) { addOption(modelList, m, m); });
    };
    if (cache[key]) return fill(cache[key]);
    var url = modelInput.dataset.models + "?kind=" + encodeURIComponent(kind) + "&brand=" + encodeURIComponent(brand);
    fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } })
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(function (models) { cache[key] = models; fill(models); })
      .catch(function () { /* öneri yoksa model yine yazılır */ });
  }

  // --- Ekran kilidi: "Yok" seçiliyken şifre alanı gizli -----------------------------------------------------------
  var lockValue = form.querySelector("[data-lock-value]");
  function syncLock() {
    var checked = form.querySelector('input[name="Input.LockType"]:checked');
    if (lockValue) lockValue.hidden = !checked || checked.value === "none";
  }

  // --- Etiket önizlemesi ------------------------------------------------------------------------------------------
  function value(name) {
    var el = form.querySelector('[data-preview="' + name + '"]');
    return el ? el.value.trim() : "";
  }

  // Boşken görünen yazı sayfanın dilinde, etiketin data-empty özelliğinden gelir.
  function show(name, text) {
    var el = form.querySelector('[data-show="' + name + '"]');
    if (!el) return;
    el.textContent = text || el.dataset.empty || "";
    el.classList.toggle("is-empty", !text);
  }

  function accessories() {
    var parts = [];
    form.querySelectorAll("[data-accessories] .check:not([hidden]) input:checked").forEach(function (box) {
      parts.push(form.querySelector('label[for="' + box.id + '"]').textContent.trim());
    });
    if (value("accessories")) parts.push(value("accessories"));
    return parts.join(", ");
  }

  function update() {
    if (form.querySelector('[data-preview="customer"]')) show("customer", value("customer"));

    // Kayıtlı bir cihaz seçildiyse etiket onu gösterir, yoksa yazılan marka ve model.
    var chosen = form.querySelector('input[name="Input.DeviceId"]:checked');
    var device = chosen && chosen.dataset.device ? chosen.dataset.device : (value("brand") + " " + value("model")).trim();
    show("device", device);
    show("imei", chosen && chosen.dataset.device ? "" : value("imei"));
    show("complaint", value("complaint"));
    show("accessories", accessories());
  }

  // --- Olaylar ----------------------------------------------------------------------------------------------------
  form.addEventListener("change", function (event) {
    var target = event.target;
    if (target === kindSelect) applyKind(kindSelect.value);
    else if (target === brandPick) syncBrand();
    else if (target === brandInput) loadModels();
    else if (target.name === "Input.LockType") syncLock();
    else if (target.name === "Input.DeviceId" && target.dataset.kind) {
      // Kayıtlı cihaz seçilince seçenekler onun türüne geçer.
      kindSelect.value = target.dataset.kind;
      applyKind(target.dataset.kind);
    }
    update();
  });
  form.addEventListener("input", update);

  if (kindSelect) applyKind(kindSelect.value);
  syncLock();
  update();
})();
