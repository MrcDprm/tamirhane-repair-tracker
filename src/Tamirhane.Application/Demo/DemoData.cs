using System.Security.Cryptography;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.Shops;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Demo;

/// <summary>Bir demo atölyenin bütün kayıtları: tek seferde eklenir.</summary>
public sealed record DemoWorkshop(
    Shop Shop, Branch Branch, IReadOnlyList<Staff> Staff,
    IReadOnlyList<Customer> Customers, IReadOnlyList<Device> Devices,
    IReadOnlyList<Part> Parts, IReadOnlyList<StockLevel> StockLevels, IReadOnlyList<StockMovement> StockMovements,
    IReadOnlyList<WorkOrder> WorkOrders, IReadOnlyDictionary<int, int> Sequences);

/// <summary>
/// Demo atölyenin örnek verisi (#36). Her kayıt domain metotlarıyla, gerçek akıştaki sırayla kurulur (kabul → inceleme →
/// teklif → karar → onarım → teslim); böylece tutarlıdır: stok düşmüş, tahsilat bakiyeyle uyumlu, geçmiş tarih sıralı.
/// Bütün iş durumları ve ekranlar (onay bekleyen, 30 günü geçen, borçlu hazır, garantisi süren, kritik stok) dolu görünür.
/// </summary>
public static class DemoData
{
    /// <summary>Demo hesaplarının adresi: ".invalid" hiçbir zaman var olmaz (RFC 2606), e-posta gitmez.</summary>
    public const string EmailDomain = "demo.tamirhane.invalid";

    public static string Email(string role) => $"demo-{role}-{Guid.NewGuid():N}@{EmailDomain}";

    /// <summary>Kimsenin bilmediği rastgele şifre: demo hesabına sadece "Demoyu dene" düğmesiyle girilir.</summary>
    public static string Password() => "Dm1-" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    public static DemoWorkshop Build(string locale, Guid ownerUserId, Guid technicianUserId, DateTimeOffset now)
    {
        var en = locale == "en";
        string L(string tr, string english) => en ? english : tr;
        // Örnek geçmiş 45 gün önce başlar; atölyenin kendisi ise şimdi açılır: demonun ömrü (temizlik, üst sınır)
        // Shop.CreatedAt'ten sayılır.
        var opened = now.AddDays(-45);

        var shop = Ok(Shop.CreateDemo(L("Örnek Teknik Servis", "Sample Repair Shop"), locale, now));
        var branch = Ok(Branch.Create(shop.Id, Branch.DefaultName(locale),
            L("Caferağa Mah. Moda Cd. No: 12, Kadıköy", "12 Moda Street, Kadıköy, Istanbul"), opened, "0216 555 01 23"));
        var owner = Ok(Staff.Create(shop.Id, ownerUserId, StaffRole.Owner, L("Selin Kaya", "Emma Clarke"), branch.Id));
        var tech = Ok(Staff.Create(shop.Id, technicianUserId, StaffRole.Technician, L("Mert Demir", "James Hill"), branch.Id));

        // Stok: açılış girişiyle. İki parça kritik seviyenin altında kalır (panelde ve stokta uyarı görünsün).
        var parts = new List<Part>();
        var levels = new List<StockLevel>();
        var movements = new List<StockMovement>();
        Part AddPart(string sku, string name, decimal cost, decimal sale, int critical, int quantity)
        {
            var part = Ok(Part.Create(shop.Id, sku, name, cost, sale, critical));
            var level = StockLevel.Create(part.Id, branch.Id);
            movements.Add(Ok(level.Receive(quantity, owner.Id, opened.AddHours(2), L("Açılış stoğu", "Opening stock"))));
            parts.Add(part);
            levels.Add(level);
            return part;
        }
        var screen13 = AddPart("IP13-SCR", L("iPhone 13 ekran paneli", "iPhone 13 display panel"), 3200, 4800, 1, 3);
        AddPart("IP11-BAT", L("iPhone 11 batarya", "iPhone 11 battery"), 650, 1200, 2, 1);
        var redmiBattery = AddPart("RN12-BAT", L("Redmi Note 12 batarya", "Redmi Note 12 battery"), 420, 850, 2, 4);
        AddPart("USBC-PORT", L("USB-C şarj soketi", "USB-C charging port"), 60, 250, 5, 12);
        var macKeyboard = AddPart("MBA-KBD", L("MacBook Air M1 klavye", "MacBook Air M1 keyboard"), 1800, 3400, 1, 2);
        AddPart("THERM-PST", L("Termal macun (4 g)", "Thermal paste (4 g)"), 90, 200, 2, 1);
        var stock = new BranchStock(branch.Id, levels);

        var customers = new List<Customer>();
        var devices = new List<Device>();
        var orders = new List<WorkOrder>();
        var sequences = new Dictionary<int, int>();
        var phone = 0;

        // İş emirleri en eskisinden başlar: numaralar tarihle birlikte artar.
        WorkOrder Receive(string name, string? email, DeviceKind kind, string brand, string model, string? imei,
            double daysAgo, string complaint, string? condition = null, string? accessories = null, string? passcode = null,
            int? promiseInDays = null)
        {
            var at = now.AddDays(-daysAgo);
            phone++;
            var customer = Ok(Customer.Create(shop.Id, name, $"0532 555 {10 + phone:00} {20 + phone:00}", email, true, at));
            var device = Ok(Device.Create(customer, kind, brand, model, null, imei));
            sequences[at.Year] = sequences.GetValueOrDefault(at.Year) + 1;
            var order = Ok(WorkOrder.Receive(shop.Id, branch.Id, WorkOrderNumber.Format(at.Year, sequences[at.Year]),
                TrackingCode.Generate(), customer.Id, device.Id, complaint, condition, accessories, passcode, owner.Id, at));
            if (promiseInDays is { } days)
                Ok(order.Promise(DateOnly.FromDateTime(at.AddDays(days).DateTime), DateOnly.FromDateTime(at.DateTime)));
            customers.Add(customer);
            devices.Add(device);
            orders.Add(order);
            return order;
        }
        DateTimeOffset At(WorkOrder order, double hours) => order.CreatedAt.AddHours(hours);
        void Labor(WorkOrder order, string description, decimal price, decimal discount = 0) =>
            Ok(order.AddQuoteLine(QuoteLineKind.Labor, null, description, 1, price, 20, discount));
        void OrderedPart(WorkOrder order, string description, decimal price) =>
            Ok(order.AddQuoteLine(QuoteLineKind.Part, null, description, 1, price, 20, 0));
        void StockPart(WorkOrder order, Part part) =>
            Ok(order.AddQuoteLine(QuoteLineKind.Part, part.Id, part.Name, 1, part.SalePrice, 20, 0));
        // Teklif kalemleri ancak inceleme başlayınca eklenebilir: incele → kalemler → gönder.
        void Diagnose(WorkOrder order, double hours) => Ok(order.StartDiagnosis(tech.Id, At(order, hours)));
        void SendQuote(WorkOrder order, double hours) => Ok(order.SendQuote(tech.Id, At(order, hours)));
        void Ready(WorkOrder order, double hours) => movements.AddRange(Ok(order.MarkReady(tech.Id, At(order, hours), stock)));

        // 30 günü geçen açık iş: parça bekliyor.
        var asus = Receive(L("Kaan Yılmaz", "Oliver Grant"), null, DeviceKind.Computer, "Asus", "VivoBook 15", null, 34,
            L("Açılmıyor, şarj ışığı yanıp sönüyor", "Won't turn on, charging light blinks"),
            accessories: L("Şarj adaptörü", "Charger"));
        Diagnose(asus, 3);
        OrderedPart(asus, L("Şarj entegresi (sipariş)", "Charging IC (ordered)"), 1400);
        Labor(asus, L("Anakart onarımı işçiliği", "Logic board repair labour"), 1600);
        SendQuote(asus, 26);
        Ok(asus.DecideQuote(true, L("Kaan Yılmaz", "Oliver Grant"), owner.Id, At(asus, 50)));
        Ok(asus.AddNote(L("Şarj entegresi yanmış, tedarikçiden sipariş verildi.", "Charging IC burnt out, ordered from the supplier."),
            false, tech.Id, At(asus, 52)));
        Ok(asus.AddNote(L("Parça siparişi verildi, gelince onarıma devam edeceğiz.", "The part is on order; we'll continue the repair when it arrives."),
            true, tech.Id, At(asus, 53)));

        // Teslim edilmiş, garantisi süren iki iş.
        var watch = Receive(L("Elif Şahin", "Sophie Turner"), "elif.sahin@example.com", DeviceKind.Watch, "Apple", "Watch SE", null, 25,
            L("Pil yarım gün dayanıyor", "Battery lasts half a day"));
        Diagnose(watch, 2);
        OrderedPart(watch, L("Apple Watch SE batarya", "Apple Watch SE battery"), 900);
        Labor(watch, L("Batarya değişimi", "Battery replacement"), 600);
        SendQuote(watch, 5);
        Ok(watch.DecideQuote(true, L("Elif Şahin", "Sophie Turner"), null, At(watch, 7)));
        Ready(watch, 50);
        Ok(watch.RecordPayment(watch.Balance, PaymentMethod.Cash, owner.Id, At(watch, 74), null));
        Ok(watch.Deliver(owner.Id, At(watch, 74), shop.WarrantyDays));

        var ipad = Receive(L("Burak Çelik", "Daniel Moore"), "burak.celik@example.com", DeviceKind.Tablet, "Apple", "iPad 9", null, 15,
            L("Ekran kırık, dokunmatik çalışıyor", "Cracked screen, touch still works"),
            condition: L("Köşede ezik", "Dent on the corner"), accessories: L("Kılıf", "Case"));
        Diagnose(ipad, 1);
        OrderedPart(ipad, L("iPad 9 ön cam", "iPad 9 front glass"), 2200);
        Labor(ipad, L("Cam değişimi işçiliği", "Glass replacement labour"), 800);
        SendQuote(ipad, 3);
        Ok(ipad.DecideQuote(true, L("Burak Çelik", "Daniel Moore"), null, At(ipad, 5)));
        Ready(ipad, 70);
        Ok(ipad.RecordPayment(ipad.Balance, PaymentMethod.Card, owner.Id, At(ipad, 96), null));
        Ok(ipad.Deliver(owner.Id, At(ipad, 96), shop.WarrantyDays));

        // İptal: müşteri teklifi bekletti, sonra vazgeçti.
        var huawei = Receive(L("Deniz Aydın", "Grace Hall"), null, DeviceKind.Phone, "Huawei", "P30", null, 10,
            L("Arka kamera bulanık çekiyor", "Rear camera takes blurry photos"));
        Diagnose(huawei, 2);
        OrderedPart(huawei, L("Arka kamera modülü", "Rear camera module"), 1900);
        Labor(huawei, L("Kamera değişimi", "Camera replacement"), 500);
        SendQuote(huawei, 6);
        Ok(huawei.Cancel(owner.Id, At(huawei, 75), L("Müşteri onarımdan vazgeçti, yeni telefon alacak.",
            "The customer decided against the repair and will buy a new phone."), stock));

        // Uzun süredir onay bekleyen teklif.
        var ps5 = Receive(L("Cem Öztürk", "Henry Ward"), "cem.ozturk@example.com", DeviceKind.Console, "Sony", "PlayStation 5", null, 8,
            L("Görüntü gelmiyor, HDMI soketi gevşek", "No picture, HDMI port is loose"), accessories: L("Güç kablosu", "Power cable"));
        Diagnose(ps5, 4);
        OrderedPart(ps5, L("HDMI soketi", "HDMI port"), 450);
        Labor(ps5, L("Soket değişimi (mikro lehim)", "Port replacement (micro soldering)"), 1200);
        SendQuote(ps5, 24);

        // Hazır ama borcu var: stoktan parça düştü, kapora alındı.
        var macbook = Receive(L("Merve Koç", "Chloe Evans"), "merve.koc@example.com", DeviceKind.Computer, "Apple", "MacBook Air M1", null, 6,
            L("Klavyede birkaç tuş basmıyor, üstüne kahve döküldü", "Several keys don't work after a coffee spill"),
            condition: L("Kasada hafif çizikler", "Light scratches on the case"), accessories: L("Şarj adaptörü", "Charger"));
        Diagnose(macbook, 2);
        StockPart(macbook, macKeyboard);
        Labor(macbook, L("Klavye değişimi ve sıvı temizliği", "Keyboard replacement and liquid cleaning"), 1100, 10);
        SendQuote(macbook, 5);
        Ok(macbook.DecideQuote(true, L("Merve Koç", "Chloe Evans"), owner.Id, At(macbook, 8)));
        Ok(macbook.RecordPayment(1000, PaymentMethod.Transfer, owner.Id, At(macbook, 9), L("Kapora", "Deposit")));
        Ready(macbook, 120);
        Ok(macbook.AddNote(L("Klavye değişti, bütün tuşlar test edildi.", "Keyboard replaced, every key tested."),
            true, tech.Id, At(macbook, 121)));

        // Teklif reddedildi: onarılmadan iade edilecek, inceleme ücreti var.
        var dyson = Receive(L("Onur Polat", "Jack Wilson"), null, DeviceKind.Appliance, "Dyson", "V11", null, 5,
            L("Çalışırken kesik kesik duruyor", "Keeps cutting out while running"));
        Diagnose(dyson, 3);
        OrderedPart(dyson, L("Motor ünitesi", "Motor unit"), 3800);
        Labor(dyson, L("Motor değişimi", "Motor replacement"), 700);
        SendQuote(dyson, 20);
        Ok(dyson.DecideQuote(false, L("Onur Polat", "Jack Wilson"), null, At(dyson, 30)));
        Ok(dyson.SetDiagnosisFee(250));

        // Onarımda: stoktan parça, kapora.
        var redmi = Receive(L("Ece Kurt", "Mia Robinson"), "ece.kurt@example.com", DeviceKind.Phone, "Xiaomi", "Redmi Note 12", "490154203237518", 4,
            L("Şarj çabuk bitiyor, telefon ısınıyor", "Battery drains fast and the phone gets hot"), passcode: "2580", promiseInDays: 5);
        Diagnose(redmi, 2);
        StockPart(redmi, redmiBattery);
        Labor(redmi, L("Batarya değişimi", "Battery replacement"), 400);
        SendQuote(redmi, 4);
        Ok(redmi.DecideQuote(true, L("Ece Kurt", "Mia Robinson"), null, At(redmi, 6)));
        Ok(redmi.RecordPayment(500, PaymentMethod.Cash, owner.Id, At(redmi, 6), L("Kapora", "Deposit")));

        // Yeni gönderilen teklif: müşteri takip sayfasından onaylayabilir.
        var iphone13 = Receive(L("Zeynep Arslan", "Olivia Bennett"), "zeynep.arslan@example.com", DeviceKind.Phone, "Apple", "iPhone 13", null, 3,
            L("Ekran kırık, dokunmatik sağ altta çalışmıyor", "Cracked screen, touch doesn't work in the bottom right"),
            condition: L("Arka cam sağlam", "Back glass intact"), accessories: L("Kılıf", "Case"), promiseInDays: 4);
        Diagnose(iphone13, 3);
        StockPart(iphone13, screen13);
        Labor(iphone13, L("Ekran değişimi işçiliği", "Screen replacement labour"), 750);
        SendQuote(iphone13, 26);

        // İncelemede: taslak teklif (müşteriye henüz görünmez).
        var iphone12 = Receive(L("Emre Yıldız", "Liam Carter"), null, DeviceKind.Phone, "Apple", "iPhone 12", null, 2,
            L("Face ID çalışmıyor", "Face ID doesn't work"), passcode: "4826");
        Ok(iphone12.StartDiagnosis(tech.Id, At(iphone12, 20)));
        Labor(iphone12, L("Face ID modülü onarımı", "Face ID module repair"), 1800);

        // Yeni gelenler.
        Receive(L("Seda Aksoy", "Ava Mitchell"), "seda.aksoy@example.com", DeviceKind.Computer, "Lenovo", "IdeaPad 5", null, 1,
            L("Fan çok ses yapıyor, ısınınca kapanıyor", "Fan is very loud and it shuts down when hot"),
            condition: L("Kasada çizik", "Scratch on the case"), accessories: L("Şarj adaptörü", "Charger"), promiseInDays: 4);
        Receive(L("Hakan Erdem", "Noah Cooper"), null, DeviceKind.Phone, "Samsung", "Galaxy A54", null, 0.1,
            L("Şarj soketi gevşek, kablo oynayınca şarj kesiliyor", "Charging port is loose; charging stops when the cable moves"),
            passcode: "1397", promiseInDays: 2);

        return new DemoWorkshop(shop, branch, [owner, tech], customers, devices, parts, levels, movements, orders, sequences);
    }

    // Örnek veri bizim yazdığımız sabit veri: bir kural tutmazsa bu bir hatadır, sessizce atlanmaz.
    private static T Ok<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException($"Demo verisi kurulamadı: {result.Error!.Code}");

    private static void Ok(Result result)
    {
        if (result.IsFailure)
            throw new InvalidOperationException($"Demo verisi kurulamadı: {result.Error!.Code}");
    }
}
