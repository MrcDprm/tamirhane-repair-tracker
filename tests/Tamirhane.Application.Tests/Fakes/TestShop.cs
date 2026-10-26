using Microsoft.Extensions.Time.Testing;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Customers;
using Tamirhane.Application.Inventory;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Application.Tests.Fakes;

/// <summary>Bir atölyenin bütün sahte bağımlılıkları ve kullanım senaryoları tek yerde.</summary>
public sealed class TestShop
{
    public TestShop(bool isOwner = true)
    {
        User = new FakeUser(Guid.NewGuid(), isOwner);
        Settings = new ShopSettings(WarrantyDays: 90, DefaultVatRate: 20);
        Orders = new FakeWorkOrders(User);
        Customers = new FakeCustomers(User);
        Devices = new FakeDevices(User);
        Parts = new FakeParts(User) { Stock = Stock };
    }

    // 20 Ekim 2026 09:00 Türkiye saati
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 20, 6, 0, 0, TimeSpan.Zero));
    public FakeUser User { get; }
    public ShopSettings Settings { get; }
    public FakeWorkOrders Orders { get; }
    public FakeCustomers Customers { get; }
    public FakeDevices Devices { get; }
    public FakeParts Parts { get; }
    public FakeStock Stock { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
    public FakeQueue Queue { get; } = new();

    public ReceiveDevice ReceiveDevice() => new(Customers, Devices, Orders, UnitOfWork, Queue, User, Clock);
    public FindCustomerByPhone FindCustomerByPhone() => new(Customers, Devices);
    public StartDiagnosis StartDiagnosis() => new(Orders, UnitOfWork, Queue, User, Clock);
    public StartRepair StartRepair() => new(Orders, UnitOfWork, Queue, User, Clock);
    public AddQuoteLine AddQuoteLine() => new(Orders, Parts, UnitOfWork, Queue, User, Clock);
    public RemoveQuoteLine RemoveQuoteLine() => new(Orders, UnitOfWork, Queue, User, Clock);
    public SendQuote SendQuote() => new(Orders, UnitOfWork, Queue, User, Clock);
    public RecordQuoteDecision RecordQuoteDecision() => new(Orders, UnitOfWork, Queue, User, Clock);
    public DecideQuoteByCustomer DecideQuoteByCustomer() => new(Orders, UnitOfWork, Queue, Clock);
    public MarkReady MarkReady() => new(Orders, Stock, UnitOfWork, Queue, User, Clock);
    public DeliverWorkOrder Deliver() => new(Orders, new FakeSettings(Settings), UnitOfWork, Queue, User, Clock);
    public CancelWorkOrder Cancel() => new(Orders, Stock, UnitOfWork, Queue, User, Clock);
    public AddNote AddNote() => new(Orders, UnitOfWork, Queue, User, Clock);
    public UpdateIntakeDetails UpdateIntake() => new(Orders, UnitOfWork, Queue, User, Clock);
    public RecordPayment RecordPayment() => new(Orders, UnitOfWork, Queue, User, Clock);
    public RecordRefund RecordRefund() => new(Orders, UnitOfWork, Queue, User, Clock);
    public SetDiagnosisFee SetDiagnosisFee() => new(Orders, UnitOfWork, Queue, User, Clock);
    public ReopenUnderWarranty ReopenUnderWarranty() => new(Orders, UnitOfWork, Queue, User, Clock);
    public AddPart AddPart() => new(Parts, Stock, UnitOfWork, User, Clock);
    public UpdatePart UpdatePart() => new(Parts, UnitOfWork);
    public ChangeStock ChangeStock() => new(Parts, Stock, UnitOfWork, User, Clock);
    public ListLowStock ListLowStock() => new(Parts, User);

    public static ReceiveDeviceCommand NewCustomerCommand(string phone = "0532 123 45 67", string complaint = "Ekran kırık") =>
        new(null, "Ayşe Kaya", phone, "ayse@example.com", true,
            null, DeviceKind.Phone, "Apple", "iPhone 13", null, "490154203237518",
            complaint, "Kasada çizik", "Kılıf", "1234");

    /// <summary>Yeni müşteriyle cihaz kabul eder ve oluşan iş emrinin kimliğini döner.</summary>
    public async Task<Guid> ReceiveAsync(string phone = "0532 123 45 67")
    {
        var result = await ReceiveDevice().HandleAsync(NewCustomerCommand(phone), default);
        return result.Value.WorkOrderId;
    }
}
