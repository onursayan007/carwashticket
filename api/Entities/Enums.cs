namespace CarWashTicket.Api.Entities;

// Sipariş durumu. Sadece OrderStateMachine değiştirir.
public enum OrderStatus
{
    Created = 0,
    AwaitingPayment = 1,
    Paid = 2,
    Redeemed = 3,
    Settled = 4,
    Failed = 5,
    Expired = 6,
    Refunded = 7
}

public enum TicketStatus
{
    Issued = 0,
    Redeemed = 1,
    Expired = 2,
    Cancelled = 3
}

// Kullanıcının istasyondaki görevi.
public enum StationRole
{
    Scanner = 0,
    Business = 1
}

// İşyerinin sunduğu yıkama biçimi.
public enum StationType
{
    SelfService = 0,
    FullService = 1,
    Both = 2
}

// Hizmetin niteliği. Self serviste adet alınır, tam hizmette paket satılır.
public enum ServiceKind
{
    // Su, köpük, cila gibi tek kullanımlık birim.
    Unit = 0,
    // Her şey dahil yıkama paketi.
    Package = 1
}

// Çift kayıtta satırın yönü.
public enum LedgerDirection
{
    Debit = 0,
    Credit = 1
}

// Hesap planı.
public enum LedgerAccount
{
    PaymentGateway = 0,
    StationRevenue = 1,
    PlatformCommission = 2,
    CustomerRefund = 3
}

public enum WebhookStatus
{
    Received = 0,
    Processed = 1,
    Failed = 2
}
