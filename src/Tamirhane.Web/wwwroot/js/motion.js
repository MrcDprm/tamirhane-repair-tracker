// Tamirhane hareket katmanı (Motion, MIT; vendor/motion.min.js). Hareket sadece anlam taşıdığı yerde:
// pano çizimi bir kez canlanır, panodaki etiketler asılırken salınır, kaşe basılınca mürekkep oturur.
// Hareket azaltma açıksa ya da kütüphane yüklenemezse hiçbir şey yapılmaz; sayfa durağan hâliyle eksiksizdir.
(function () {
  var M = window.Motion;
  if (!M || matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  var animate = M.animate, stagger = M.stagger;

  // SVG içinde döndürme, çizimin kendi koordinatlarına göre yapılsın (ip ya da kanca noktası).
  function pivot(el, x, y) {
    el.style.transformBox = "view-box";
    el.style.transformOrigin = x + "px " + y + "px";
  }

  // 1. Tezgâh çizimi (giriş, kayıt, davet): aletler kancalarına asılır, telefon gelir, etiket ipinde sallanır, kaşe basılır.
  var scene = document.querySelector(".scene");
  if (scene) {
    var hanging = scene.querySelectorAll("[data-anim=tool], [data-anim=phone]");
    animate(hanging, { y: [-36, 0], opacity: [0, 1] },
      { delay: stagger(0.09, { startDelay: 0.15 }), type: "spring", bounce: 0.4, duration: 0.9 });

    var tag = scene.querySelector("[data-anim=tag]");
    if (tag) {
      pivot(tag, 516, 142);
      animate(tag, { rotate: [-28, 9, -5, 2.5, 0], opacity: [0, 1, 1, 1, 1] }, { delay: 0.75, duration: 1.8, ease: "easeOut" });
    }
    var stamp = scene.querySelector("[data-anim=stamp]");
    if (stamp) {
      pivot(stamp, 572, 242);
      animate(stamp, { scale: [1.8, 0.94, 1], opacity: [0, 1, 1] }, { delay: 1.6, duration: 0.35, ease: [0.2, 0.8, 0.2, 1] });
    }
  }

  // 2. Panodaki etiketler (panel): kancaya asılırken salınır, kendi eğikliğinde durur.
  var tags = document.querySelectorAll(".hang-tag");
  tags.forEach(function (el, i) {
    var tilt = parseFloat(getComputedStyle(el).getPropertyValue("--tilt")) || 0;
    animate(el, { rotate: [tilt + 14, tilt - 6, tilt + 3, tilt], opacity: [0, 1, 1, 1] },
      { delay: 0.08 * i, duration: 1.2, ease: "easeOut" });
  });

  // 3. Basılan kaşe (kabul ve durum değişikliği sonrası): büyükten iner, hafif ezilip oturur.
  document.querySelectorAll(".stamp.is-pressed").forEach(function (el) {
    el.classList.remove("is-pressed"); // CSS yedeği yerine Motion'ın yaylı hareketi
    animate(el, { scale: [1.7, 0.92, 1], opacity: [0, 1, 1], rotate: [-10, -3, -4] }, { duration: 0.42, ease: [0.2, 0.8, 0.2, 1] });
  });

  // 4. Panodaki aletler (yan menü): fareyle üstüne gelince alet kancasında hafifçe sallanır.
  document.querySelectorAll(".side-tools [data-anim=tool]").forEach(function (el) {
    var box = el.getBBox();
    pivot(el, box.x + box.width / 2, box.y - 4);
    el.addEventListener("pointerenter", function () {
      animate(el, { rotate: [0, 7, -5, 3, 0] }, { duration: 0.9, ease: "easeOut" });
    });
  });
})();
