// Trusted Types "default" politikası (CSP: require-trusted-types-for 'script'). htmx sunucudan gelen HTML parçasını
// sayfaya innerHTML ile koyar; bu parça bizim Razor görünümümüzdür ve içindeki kullanıcı verisi sunucuda kaçışlanmıştır.
// Sadece HTML'e izin verilir: createScript ve createScriptURL tanımlı değil, yani eval ve dışarıdan betik adresi
// her durumda engellenir. Trusted Types'ı desteklemeyen tarayıcıda (Firefox) hiçbir şey yapmaz.
if (window.trustedTypes && trustedTypes.createPolicy) {
  trustedTypes.createPolicy("default", {
    createHTML: function (html) { return html; },
  });
}
