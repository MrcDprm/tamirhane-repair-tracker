# SEO (#42)

Amaç: tamir atölyesi için iş takip programı arayan biri Tamirhane'yi bulsun. Dizine sadece tanıtım ve yasal sayfalar
girer (`/`, `/privacy`, `/terms` ve `/en/...` karşılıkları); uygulama, hesap sayfaları ve müşteri takip linkleri girmez.

## Anahtar kelimeler

**Türkçe**

- Genel (rekabetli): teknik servis programı, servis takip programı, tamir takip programı, teknik servis yazılımı
- Ücretsiz arayanlar: ücretsiz teknik servis programı, ücretsiz servis takip programı, bedava tamir takip programı
- Atölye türü: telefon tamircisi programı, cep telefonu servis programı, bilgisayar tamir takip programı, beyaz eşya servis programı
- İşin kendisi: iş emri takip programı, cihaz kabul fişi programı, servis fişi programı, arıza takip programı, teklif onayı
- Müşteri tarafı: müşteri cihaz takip sayfası, tamir durum sorgulama, servis durumu takip linki

**English**

- General: repair shop software, repair shop management software, repair ticketing software, repair tracking software
- Free: free repair shop software, free repair shop management software, free repair ticket system
- Shop type: phone repair shop software, cell phone repair software, computer repair shop software, appliance repair software
- The job: work order tracking for repair shops, device check-in receipt, repair quote approval, parts inventory for repair shop
- Customer side: repair status tracking page, customer repair status updates

Kelimeler metne doğal cümle olarak girer, sıralanıp doldurulmaz.

## Sayfalarda

| Yer | Türkçe | English |
|---|---|---|
| Başlık (`/`) | Tamirhane: Ücretsiz teknik servis ve tamir takip programı | Tamirhane: Free repair shop software and repair tracking |
| Açıklama (`/`) | Tamir atölyeleri için ücretsiz servis takip programı: cihaz kabul fişi, iş emri, teklif onayı, parça stoku; müşteri cihazını telefondan takip eder. | Free repair shop software: check-in receipts, work orders, quote approval and parts stock. Customers follow their device's repair on their phone. |
| `h1` | Tamir atölyesi için iş emri takibi | Work order tracking for repair shops |

- Her sayfada `<title>`, meta açıklama (160 karakterden kısa, test denetler), canonical, `hreflang` (tr, en, x-default),
  Open Graph ve Twitter kartı (`/img/og-tr.png`, `/img/og-en.png`, 1200×630), tek `h1`.
- Tanıtım sayfasında JSON-LD `SoftwareApplication` (ücretsiz, web, iki dil).
- Dil adreste: Türkçe önsüz, İngilizce `/en`. Tarayıcı dili adres seçmez; arama motoru her adreste tek dil görür.
- `robots.txt` uygulama yollarını kapatır, `sitemap.xml` altı adresi iki dilin karşılıklarıyla listeler.
- Uygulama, hesap ve hata sayfalarında `noindex`; takip sayfasında `noindex, nofollow` ve `X-Robots-Tag`.

## Repo ve dışarıdaki metinler

- GitHub açıklaması: "Free repair shop software: work orders, check-in receipts, quote approval, parts stock and a
  customer tracking page. ASP.NET Core MVC, Clean Architecture."
- Topics: repair-shop, repair-shop-software, work-order, ticketing, aspnet-core, clean-architecture, csharp, postgresql,
  htmx, multi-tenant, web-push
- README'nin ilk paragrafı aynı ifadeleri taşır (#41).

## Search Console (kullanıcı yapar)

1. https://search.google.com/search-console → Mülk ekle → **Alan adı** `miracdeprem.com` zaten doğrulanmışsa
   tamirhane alt alanı onun altında görünür; değilse **URL öneki** `https://tamirhane.miracdeprem.com/`.
2. URL öneki seçildiyse doğrulama: HTML etiketi yerine **DNS (TXT kaydı)** önerilir (Vercel DNS'e eklenir).
3. Dizin oluşturma → Site haritaları → `sitemap.xml` gönder.
4. URL denetimi → `https://tamirhane.miracdeprem.com/` ve `/en/` için "Dizine eklenmesini iste".
5. Birkaç gün sonra Sayfalar raporunda iki dilin de "Dizine eklendi" olduğuna, hreflang uyarısı olmadığına bakılır.
