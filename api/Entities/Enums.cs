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
    Staff = 0,
    Manager = 1
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
