# Tamirhane: v1 planı

Teknik servis ve tamir atölyeleri için iş emri takip sistemi. ASP.NET Core MVC, Clean Architecture, PostgreSQL. Lisans AGPL-3.0. Gelecek Planları yok: aşağıdakilerin hepsi v1.0.0'dan önce biter. Ücret ya da şirket gerektiren özellik (SMS, e-Arşiv fatura, online ödeme) kapsamda yok.

## Kim, nerede, ne için

- **Atölye sahibi ve teknisyen:** Telefon, bilgisayar, tablet, küçük ev aleti tamir eden 1-10 kişilik dükkân. Tezgâhta ayakta, gündüz ışığında ya da floresan altında çalışıyor; müşteri karşısında beklerken cihazı kaydetmek, ya da "cihazım ne oldu" diye gelen müşterinin işini birkaç saniyede bulmak istiyor.
- **Müşteri:** Cihazını bırakmış, telefonundan takip linkini açıyor. Hesap açmak istemiyor; tek istediği "ne durumda, ne kadar tutacak, ne zaman alabilirim".
- **Rakipler:** RepairShopr (kullanıcı başı ~60 $/ay), RepairDesk (mağaza başı 99-149 $/ay), Türkiye'de Bilsoft ve benzeri masaüstü/bulut programlar. Ortak çekirdek: cihaz kabul, iş emri durumu, müşteriye bildirim, teklif onayı, parça stoku, kabul fişi. Fark: ücretsiz barındırılan sürüm, giriş gerektirmeyen takip sayfası, Türkçe, açık kaynak.

## Kapsam

**Atölye ve kullanıcılar**
- Çok işletmeli (multi-tenant) kayıt: her atölyenin verisi tamamen ayrı.
- Şubeler: stok ve iş emirleri şubeye bağlı; tek şubeli atölye şubeyi hiç görmez.
- Roller: sahip (her şey) ve teknisyen (iş emri, stok kullanımı; ayarlar, personel ve ciro yok). Personel e-postayla davet edilir.
- E-posta doğrulamalı kayıt, şifre sıfırlama, hesap ve atölye silme.

**Cihaz kabulü**
- Müşteri: ad, telefon, e-posta (isteğe bağlı), KVKK aydınlatma onayı. Telefonla arama; aynı numarayla gelen müşteri tekrar yazılmaz.
- Cihaz: tür, marka, model, seri no / IMEI (IMEI'de Luhn kontrolü), teslim alınan aksesuarlar, görünür durum (çizik, kırık...), müşterinin şikâyeti, ekran kilidi/şifre bilgisi isteğe bağlı ve iş emri kapanınca silinir.
- Yazdırılabilir A5 kabul fişi (tarayıcının yazdırma özelliği, `@page` CSS; PDF kütüphanesi yok): atölye bilgileri, cihaz, şikâyet, şartlar, imza alanları, takip linkine giden QR kod.
- Cihaza yapıştırılacak etiket: iş emri numarası + QR kod, küçük etiket yazıcısına uygun boyutta.

**İş emri**
- Durum akışı (aşağıda), her geçiş zaman ve personelle kaydedilir; geçersiz geçiş domain katmanında engellenir.
- Teklif: parça (stoktan seçilir) ve işçilik kalemleri, KDV oranı kalem başına (varsayılan %20), iskonto. Müşteri takip linkinden onaylar ya da reddeder; onaylayan adı ve zamanı kaydedilir.
- Parça stoku: şube başına miktar, kritik seviye; onarımda kullanılan parça stoktan düşer, iptalde geri döner. Stok hareketleri listesi.
- Tahsilat: nakit, kart (POS), havale; kısmi ödeme, kalan borç. Para sistemin içinden geçmez, sadece kayıt.
- Garanti: teslimde garanti süresi başlar (atölye ayarı, varsayılan 90 gün). Garanti içinde geri gelen cihaz yeni iş emrine "garanti dönüşü" olarak bağlanır; işçilik ücretsiz önerilir.
- Teknisyen notları (müşteri görmez) ve müşteriye görünen notlar ayrı.

**Müşteriyle iletişim**
- Giriş gerektirmeyen takip sayfası: `/t/{kod}`. Durum zaman çizelgesi, teklif ve onay düğmeleri, tahmini teslim, atölyenin adresi ve telefonu.
- Her durum değişiminde e-posta (Resend, ücretsiz katman).
- Tarayıcı bildirimi (Web Push, VAPID): müşteri takip sayfasında "durum değişince haber ver" der.
- "WhatsApp'tan bildir" düğmesi: hazır mesaj + takip linkiyle `wa.me` bağlantısı açar, teknisyen tek tıkla gönderir.

**Panel ve genel**
- Açık işler, onay bekleyenler, teslime hazırlar, 30 günü geçen bekleyenler, aylık ciro (sadece sahip), kritik stok.
- Hızlı arama: ad, telefon, IMEI, iş emri numarası.
- Türkçe ve İngilizce, açık ve koyu tema.
- Herkese açık demo atölye (örnek veriyle, salt okunur değil; gece sıfırlanır).
- Tanıtım sayfası, gizlilik/KVKK aydınlatma metni, kullanım koşulları.

## Mimari

```
src/
  Tamirhane.Domain          varlıklar, değer nesneleri, iş kuralları, domain hataları
  Tamirhane.Application     kullanım senaryoları, arayüzler (depo, saat, e-posta, push, mevcut kullanıcı/atölye)
  Tamirhane.Infrastructure  EF Core + Npgsql, Identity, Resend, Web Push, QR üretimi
  Tamirhane.Web             MVC controller'lar, view'lar, kimlik doğrulama, yerelleştirme, güvenlik başlıkları
tests/
  Tamirhane.Domain.Tests        kurallar (durum makinesi, teklif toplamı, stok, garanti)
  Tamirhane.Application.Tests   kullanım senaryoları, sahte depolarla
  Tamirhane.Architecture.Tests  bağımlılık kuralı (NetArchTest) + entegrasyon (WebApplicationFactory + Testcontainers PostgreSQL)
```

**Bağımlılık kuralı:** Domain hiçbir projeye ve hiçbir NuGet paketine bağlı değil. Application sadece Domain'e bağlı; EF Core'u, ASP.NET'i, Identity'yi tanımaz. Infrastructure Application'daki arayüzleri uygular. Web, Infrastructure'ı sadece `Program.cs`'te servis kaydı için referans alır (composition root). Mimari testleri bu kuralların bozulmasını derlemede değil testte yakalar:
- Domain → hiçbir `Tamirhane.*` projesi ve `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore` yok.
- Application → `Infrastructure`, `Web`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore` yok.
- Controller'lar `Tamirhane.Infrastructure` tiplerini kullanmaz.

**Kararlar:**
- **MediatR yok:** 2025'te ticari lisansa geçti. Her kullanım senaryosu tek sınıf (`ReceiveDeviceHandler` gibi), Web'de doğrudan enjekte edilir. Doğrulama senaryonun içinde; dış kütüphane yok.
- **Sonuç tipi:** Beklenen hatalar (geçersiz geçiş, stok yetersiz, bulunamadı) istisna değil `Result<T>` döner; controller bunu doğru HTTP cevabına ve kullanıcı mesajına çevirir. Beklenmeyen hatalar genel hata sayfasına gider.
- **Zengin domain:** Durum değişikliği `workOrder.Status = ...` ile değil `workOrder.StartRepair(technician, now)` gibi metotlarla; kurallar varlığın içinde. Setter'lar `private`.
- **Saat soyutlaması:** .NET'in `TimeProvider`'ı; garanti ve "30 günü geçen" kuralları testte sahte saatle.
- **Çok kiracılık:** Her kiracıya ait tabloda `ShopId`. EF Core global query filter `ICurrentShop`'tan; yazarken `ShopId` sunucuda atanır, formdan asla alınmaz. Entegrasyon testi: A atölyesinin kullanıcısı B'nin iş emrini id ile isteyince 404.
- **Kimlikler:** Bütün birincil anahtarlar `Guid` (v7, sıralı). Ekranda görünen iş emri numarası atölye başına sıralıdır (`2026-0042`); erişim için hiçbir yerde kullanılmaz, sadece okunabilirlik içindir.
- **Takip kodu:** 12 karakter Crockford base32 (60 bit rastgele), veritabanında benzersiz indeks; kodla gelen istekler IP başına sınırlı.
- **Para:** `decimal(18,2)`, TL. KDV kalem başına; toplamlar domain'de hesaplanır, formdan gelen toplam kullanılmaz.
- **Yazdırma:** Kabul fişi ve etiket ayrı yazdırma sayfaları (`@media print`, `@page size: A5`); PDF kütüphanesi ve sunucu tarafı render yok.
- **QR:** QRCoder (MIT), SVG çıktısı.
- **Web Push:** `Lib.Net.Http.WebPush` NuGet paketi (MIT; .NET 10'da ek kripto kütüphanesi gerektirmez), VAPID anahtarları ortam değişkeninde. (İlk planda `WebPush` yazıyordu; o paket MPL-2.0 lisanslı ve 2021'den beri güncellenmeyen Portable.BouncyCastle'a bağlı olduğu için #31'de değiştirildi.)
- **Entegrasyon testleri:** Testcontainers her test çalıştırmasında Docker'da geçici bir PostgreSQL açar, testler bitince siler. Gerçek veritabanına hiç dokunulmaz; kiracı filtresi ve migration'lar gerçek PostgreSQL'de denenir. Docker Desktop gerekir (2026-10-20 kararı: kurulacak).
- **Arka plan işleri:** E-posta ve push gönderimi `Channel` tabanlı bir `BackgroundService` kuyruğunda; istek beklemez, hata loglanır ve 3 kez denenir.

## Veri modeli (özet)

```
Shop            (Id, Name, Phone, Address, WarrantyDays, DefaultVatRate, Locale, CreatedAt)
Branch          (Id, ShopId, Name, Address)
Staff           (Id, ShopId, UserId, Role: Owner|Technician, DisplayName)
Customer        (Id, ShopId, Name, Phone, Email?, KvkkConsentAt)
Device          (Id, ShopId, CustomerId, Kind, Brand, Model, Serial?, Imei?)
WorkOrder       (Id, ShopId, BranchId, Number, TrackingCode, CustomerId, DeviceId,
                 Status, Complaint, Condition, Accessories, Passcode?, CustomerNote?,
                 EstimatedReadyAt?, DeliveredAt?, WarrantyUntil?, WarrantyOfId?, CreatedAt)
StatusChange    (Id, WorkOrderId, From, To, StaffId?, At, Note?)
QuoteLine       (Id, WorkOrderId, Kind: Part|Labor, PartId?, Description, Quantity, UnitPrice, VatRate, Discount)
QuoteDecision   (WorkOrderId, Decision: Approved|Declined, ByName, At)
Part            (Id, ShopId, Sku, Name, CostPrice, SalePrice, CriticalLevel)
StockLevel      (PartId, BranchId, Quantity)
StockMovement   (Id, PartId, BranchId, Change, Reason: Purchase|Used|Returned|Adjustment, WorkOrderId?, At)
Payment         (Id, WorkOrderId, Amount, Method: Cash|Card|Transfer, At, StaffId)
PushSubscription(Id, WorkOrderId, Endpoint, P256dh, Auth, CreatedAt)
Invitation      (TokenHash, ShopId, Email, Role, ExpiresAt)
```

Identity tabloları (`AspNetUsers`...) Infrastructure'da; Domain'deki `Staff` kullanıcıyı sadece `UserId` ile bilir.

## İş emri durum makinesi

```
Received ──► Diagnosing ──► AwaitingApproval ──► InRepair ──► Ready ──► Delivered
   │             │                │   │                         ▲
   │             │                │   └── (red) ──► Declined ───┘ (cihaz onarılmadan iade)
   │             └── (teklif gerekmez, garanti/ücretsiz) ──► InRepair
   └── herhangi bir açık durumdan ──► Cancelled
```

- `AwaitingApproval`'a geçmek için en az bir teklif kalemi gerekir. Teklif onaylanınca `InRepair`'e geçilebilir; reddedilirse `Declined` (cihaz onarılmadan teslime hazır, varsa inceleme ücreti).
- `InRepair` → `Ready`: kullanılan parçalar stoktan düşer (yetersizse geçiş olmaz).
- `Ready` → `Delivered`: kalan borç sıfır olmalı ya da sahip "borçlu teslim" onayı verir. Teslimde garanti başlar, cihaz şifresi silinir.
- `Cancelled` ve `Delivered` son durumdur; hiçbir geçiş yok. Teslimden sonra sorun → yeni iş emri (`WarrantyOfId`).
- Her geçiş `StatusChange` satırı + müşteri bildirimi (e-posta, push) üretir.

## Kullanım senaryoları (Application)

Kabul: `ReceiveDevice`, `FindCustomerByPhone`, `UpdateIntakeDetails`. İş emri: `StartDiagnosis`, `AddQuoteLine`, `RemoveQuoteLine`, `SendQuote`, `DecideQuote` (takip sayfasından), `StartRepair`, `MarkReady`, `Deliver`, `Cancel`, `ReopenUnderWarranty`, `AddNote`. Stok: `AddPart`, `ReceiveStock`, `AdjustStock`, `ListLowStock`. Tahsilat: `RecordPayment`. Takip: `GetTrackingView`, `SubscribePush`. Atölye: `RegisterShop`, `InviteStaff`, `AcceptInvitation`, `AddBranch`, `UpdateShopSettings`, `DeleteShop`. Panel: `GetDashboard`, `Search`.

## Güvenlik kontrol listesi

- Identity: şifre en az 10 karakter + harf ve rakam, sık şifre listesi; 5 hatada 15 dk kilit; e-posta doğrulanmadan atölye kullanılamaz; `__Host-` önekli, HttpOnly, Secure, SameSite=Lax çerez; güvenlik damgası her istekte (şifre değişince diğer oturumlar kapanır).
- Yetki her istekte sunucuda: rol politikaları + kiracı filtresi. Teknisyen ciro ve ayar uçlarına giremez (test).
- Bütün POST'larda antiforgery (`action` yazılı formlarda da; view tarayan regresyon testi, Sahaf'taki hatadan).
- Overposting yok: her form kendi giriş modeliyle; `ShopId`, durum, toplamlar asla formdan alınmaz.
- Rate limit (`CF-Connecting-IP`; Render önünde Cloudflare, Crypto Toolkit dersi): giriş, kayıt, şifre sıfırlama, davet, takip sayfası, teklif kararı, push aboneliği.
- Kişisel verinin görünürlüğü: atölye panelinde (giriş yapmış personel) müşterinin tam adı, telefonu ve e-postası her yerde görünür. Giriş gerektirmeyen takip sayfası ise iletilebilir bir link olduğu için asgari bilgi gösterir: kargo takip sayfalarındaki gibi her ismin ilk iki harfi, gerisi yıldız ("Ayşe Kaya" → "Ay** Ka**"). Görünen harf sayısı en fazla 2, ama her zaman en az bir harf gizli kalır: "Ali" → "Al*", "Su" → "S*"; maskeleme sunucuda yapılır, tam ad sayfaya hiç gönderilmez. Telefon ve e-posta hiç görünmez; cihaz şifresi hiçbir yerde.
- E-posta linkleri `Host` başlığından değil `PUBLIC_ORIGIN` ayarından (Casus dersi); `AllowedHosts`.
- CSP: `default-src 'self'`, satır içi script yok, Trusted Types; HSTS, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `frame-ancestors 'none'`. Yazdırma sayfaları da aynı CSP ile.
- CORS yok. Production'da genel hata sayfası, stack trace yok; loglarda e-posta, telefon, IP yok.
- Gizli bilgiler (bağlantı dizesi, Resend, VAPID) ortam değişkeninde; repoda `.env.example`.
- KVKK: aydınlatma metni, müşteri kaydında açık onay zamanı, atölye silinince bütün verisi silinir; teslim edilen iş emrinde cihaz şifresi silinir.
- Bağımlılıklar yayından önce `dotnet list package --vulnerable`.

## Sprintler (1 hafta)

Her madde bir issue; her issue bir branch ve PR (`Closes #n`).

1. **Çekirdek:** Domain varlıkları ve durum makinesi, teklif toplamları, stok ve garanti kuralları; Application kullanım senaryoları (sahte depolarla); mimari testleri.
2. **Altyapı ve hesaplar:** EF Core + PostgreSQL, migration'lar, kiracı filtresi, Identity, kayıt + e-posta doğrulama, personel daveti, şubeler, roller; entegrasyon testleri (Testcontainers).
3. **Atölye ekranları:** Cihaz kabul, iş emri sayfası (durum, teklif, notlar), stok, tahsilat, kabul fişi ve etiket yazdırma, hızlı arama.
4. **Müşteri tarafı:** Takip sayfası, teklif onayı, e-posta bildirimleri, Web Push, WhatsApp düğmesi.
5. **Panel ve yayın:** Panel, garanti dönüşü, demo atölye, tanıtım ve yasal sayfalar, TR/EN, tema, son tarama, Docker + Render + Neon + Resend, README, v1.0.0, SEO.

## Tasarım planı

**Konu, kitle, iş:** Tezgâhta ayakta çalışan teknisyen, aydınlık ortam (Operate); telefonundan bakan müşteri (Read). Görsel dil tamir atölyesinin kendi eşyalarından geliyor: cihaza iple bağlanan **karton iş emri etiketi** (delikli, numaralı), tezgâhtaki kaşe ve ıstampa, parça kutularının üstündeki etiketler. Monitör, devre kartı ya da "teknoloji" klişesi yok.

**Renkler (açık tema varsayılan, atölye aydınlık):**

| Ad | Hex | Görev |
|---|---|---|
| Tezgâh | `#ECEEEB` | Zemin: soğuk açık gri (krem değil) |
| Etiket kartonu | `#E6D3A8` | Sadece iş emri etiketi yüzeyi; başka hiçbir yerde |
| Mürekkep | `#1F2A30` | Metin |
| Istampa mavisi | `#2B44A8` | Durum kaşeleri ve tek birincil eylem |
| Uyarı turuncusu | `#C8571B` | Kritik stok, 30 günü geçen iş, borç |
| Kurşun | `#69707A` | İkincil metin, çizgiler |

Koyu tema (akşam kapanış sayımı): zemin `#1B2024`, etiket `#4A4130`, metin `#E7E9E6`, ıstampa `#7C93F0`. Parlama yok.

**Yazı tipleri (2026-10-20 doğrulandı: üçü de Google Fonts'ta, `latin-ext` alt kümesiyle Türkçe harfler var, lisans OFL; dosyalar yerelden sunulacak):**
- **Barlow Semi Condensed** (OFL): başlıklar ve iş emri numaraları. Dar ve endüstriyel; etiket ve parça kutusu yazılarının dünyasından.
- **Public Sans** (OFL): gövde ve form. Okunaklı, nötr; Operate yüzeyi için.
- **Red Hat Mono** (OFL): sadece gerçek kodlar (IMEI, seri no, takip kodu); küçük etiketler için kullanılmaz.

Önceki projelerden tekrar yok: Atkinson Hyperlegible (Nabız), Bricolage Grotesque / Source Sans 3 / JetBrains Mono (Zarf), Geist (Crypto Toolkit), Fraunces / Manrope (Seans).

**Yerleşim (atölye):**

```
┌──────────────────────────────────────────────────────────────────────┐
│ Tamirhane   [ Ad, telefon, IMEI ya da iş emri no ara......... ]  [Cihaz kabul et] │
├──────────────────────────────────────────────────────────────────────┤
│ Bugün                                                                │
│  Onay bekleyen 3   Teslime hazır 5   Onarımda 8   30 günü geçen 1    │  ← tıklanınca liste süzülür
├──────────────────────────────────────────────────────────────────────┤
│ No         Cihaz         Arıza         Müşteri       Durum          Bekleme │
│ 2026-0042  iPhone 13     Kırık ekran   Ayşe Kaya     Onarımda       2 gün   │
│ 2026-0041  Lenovo T14    Fan sesi      Mert Demir    Hazır          bugün   │  ← tablo, kart değil
│ 2026-0040  Dyson V11     Şarj olmuyor  Selin Arslan  Onay bekliyor  4 gün   │
└──────────────────────────────────────────────────────────────────────┘
```

İş emri sayfası: solda etiket (karton yüzey, delik, numara, cihaz, şikâyet, kaşeler), sağda işlem sütunu (sıradaki geçiş tek birincil düğme, teklif tablosu, tahsilat, notlar). Sola yaslı, tablolar tam genişlik.

**Müşteri takip sayfası (telefon):** En üstte etiketin kendisi: atölyenin adı, iş emri numarası, cihaz. Altında geçilen her durum, etiketin üstüne basılmış bir kaşe izi olarak, tarih ve saatiyle. Teklif varsa kalemler ve iki düz düğme: "Onarımı onayla" / "Onarım istemiyorum".

**İlkeler:**
- Cesaret tek yerde: **kaşe.** Her durum geçişi etikete ıstampa mavisiyle, hafif eğik ve hafif dokulu bir kaşe olarak basılır (SVG + CSS maske, gerçek kaşe izinden türetilmiş doku). Atölyede durum değiştirildiğinde tek bir "basılma" hareketi; `prefers-reduced-motion`'da hareketsiz. Kaşe metni büyük harf (kaşelerin kendisi öyle); uygulamanın başka hiçbir yerinde büyük harf etiket yok.
- Geri kalan her şey sakin: tablolar, düz düğmeler, iki seviye köşe yuvarlaklığı (etiket ve düğme farklı), kart içinde kart yok.
- Renk nadir: ıstampa mavisi sadece kaşe ve birincil eylem; turuncu sadece gerçek bir uyarı.
- Yazılar düz: "Cihaz kabul et", "Onarıma başla", "Teslim et". Boş liste: "Bugün açık iş yok. Yeni bir cihaz geldiğinde Cihaz kabul et'e bas."
- Dokunma hedefleri en az 44 px (tezgâhta, eldivenli el); klavye kısayolları (`/` arama, `N` yeni kabul).

**Varsayılanlara karşı kontrol (revizyon):**
- İlk akla gelen "koyu zemin + neon turuncu, teknik servis = teknoloji" görünümüydü (iki numaralı klişe). Değiştirildi: atölyenin kendi eşyaları (karton etiket, kaşe), aydınlık zemin.
- İkinci aday, iş emirlerini yuvarlak köşeli kartlardan oluşan bir kanban panosu olarak göstermekti (SaaS kart kiti). Değiştirildi: liste tablo; "etiket" görünümü sadece tek iş emri sayfasında ve müşteri sayfasında.
- Etiket kartonunun rengi kreme kaymasın diye daha doymuş, gerçek manila kartonuna yakın tutuldu ve zemin olarak hiç kullanılmıyor.

**Revizyon 2 (2026-10-26): delikli pano ve hareket katmanı.** İlk ekranlar düz form ve tablodan ibaretti; atölye hissi yoktu. Renkler, yazı tipleri ve kaşe aynı kaldı, üstüne:
- **İmza öğesi delikli pano (pegboard):** yan menü koyu, delikli bir pano; bulunulan sayfa panoya asılı karton etiket. Altında asılı aletler (tornavida, kargaburun, cımbız).
- **Giriş, kayıt, şifre ve davet sayfaları ikiye bölünür:** solda pano çizimi (asılı aletler, ekranı çatlak telefon, iple bağlı iş emri etiketi ve kaşesi, tezgâh), sağda form.
- **Paneller:** sayfalar tezgâh zemininde paneller (başlıklı, gölgeli); kart içinde kart yok. Panel sayıları (#33) panoya iple asılı etiketler. Boş durumda panoda boş kanca çizimi.
- **Kabul ekranı:** numaralı adımlar (müşteri, cihaz, arıza) ve sağda yazdıkça dolan etiket önizlemesi.
- **Hareket (Motion, MIT; GSAP AGPL lisansla uyumsuz olduğu için seçilmedi):** pano çizimi bir kez canlanır (aletler asılır, etiket ipinde sallanır, kaşe basılır), panodaki etiketler asılırken salınır, kaşe yaylı basılır, yan menüdeki aletler üstüne gelince sallanır. Sayfa geçişleri View Transitions ile (kabuldeki etiket iş emri sayfasındaki etikete taşınır; Firefox'ta sayfa normal açılır). Kısmi sayfa güncellemesi htmx ile #22'de (durum değişince sadece etikete kaşe basılır). Hareket azaltma açıksa hiçbiri çalışmaz.
- Her yerde hareket yok: kayarak beliren bölümler gibi kalıp hareketler bilerek kullanılmadı.
