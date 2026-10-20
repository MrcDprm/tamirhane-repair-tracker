[English](README.md) | Türkçe

# Tamirhane

Teknik servis ve tamir atölyeleri için iş emri takip sistemi: cihaz kabulü, onarım durumu, fiyat teklifi ve onayı, parça stoku, garanti takibi ve müşteriye giriş gerektirmeyen takip linki. ASP.NET Core MVC ile Clean Architecture üzerine kuruldu.

> **Durum:** Geliştiriliyor. Bu belge planı anlatıyor; özellikler tamamlandıkça işaretlenecek.

## Özellikler

**Atölye ve kullanıcılar**
- [ ] Çok işletmeli kayıt: her atölyenin verisi tamamen ayrı
- [ ] Şubeler: stok ve iş emirleri şubeye göre
- [ ] Roller: sahip ve teknisyen; e-posta ile personel daveti
- [ ] E-posta doğrulamalı kayıt, şifre sıfırlama

**Cihaz kabulü**
- [ ] Müşteri ve cihaz kaydı: marka, model, seri no / IMEI, teslim alınan aksesuarlar, cihazın görünür durumu, müşterinin şikâyeti
- [ ] Yazdırılabilir A5 kabul fişi; üzerinde takip linkine giden QR kod
- [ ] Cihaza yapıştırılacak barkod / QR etiketi

**İş emri**
- [ ] Durum akışı: Kabul → İnceleme → Onay bekliyor → Onarımda → Hazır → Teslim edildi; iptal ve iade. Geçersiz geçişler engellenir
- [ ] Fiyat teklifi: parça ve işçilik kalemleri, KDV; müşteri teklifi takip linkinden onaylar ya da reddeder
- [ ] Parça stoku: kullanılan parça stoktan düşer, kritik seviyede uyarı
- [ ] Tahsilat kaydı: nakit, kart, havale; kalan borç
- [ ] Garanti: teslimden sonra garanti süresi işler; garanti içinde geri gelen cihaz önceki iş emrine bağlanır

**Müşteriyle iletişim**
- [ ] Giriş gerektirmeyen takip sayfası: tahmin edilemez kod, durum zaman çizelgesi
- [ ] Her durum değişiminde e-posta bildirimi
- [ ] Tarayıcı bildirimi (Web Push): müşteri takip sayfasında "haber ver" der
- [ ] "WhatsApp'tan bildir" düğmesi: hazır mesaj ve takip linkiyle tek tıkla

**Panel**
- [ ] Açık işler, onay bekleyenler, teslime hazır cihazlar, aylık ciro
- [ ] Türkçe ve İngilizce, açık ve koyu tema
- [ ] Örnek verili herkese açık demo atölye

## Mimari

Clean Architecture: bağımlılıklar yalnızca içe doğru akar.

```
Tamirhane.Web  ──►  Tamirhane.Application  ──►  Tamirhane.Domain
      │                      ▲
      └──► Tamirhane.Infrastructure (yalnızca Program.cs'te servis kaydı için)
```

| Proje | Görevi |
|---|---|
| `Tamirhane.Domain` | Varlıklar ve iş kuralları (ör. iş emri durum akışı). Hiçbir projeye ve çatıya bağımlı değil. |
| `Tamirhane.Application` | Kullanım senaryoları; depo, e-posta, saat gibi dış hizmetlerin arayüzleri. |
| `Tamirhane.Infrastructure` | Bu arayüzlerin uygulamaları: EF Core + PostgreSQL, e-posta, Web Push. |
| `Tamirhane.Web` | ASP.NET Core MVC: controller'lar, view'lar, kimlik doğrulama. |

Bağımlılık kuralı `Tamirhane.Architecture.Tests` içinde testle doğrulanır.

## Kullanılan Teknolojiler

- C#, ASP.NET Core MVC, Entity Framework Core, PostgreSQL
- ASP.NET Core Identity
- xUnit, NetArchTest
- Docker, Render

## Kurulum ve Çalıştırma

Yazılacak.

## Öğrendiklerim

Proje bitince yazılacak.
