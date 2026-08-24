namespace CarWashTicket.Api.Entities;

// Müşterinin bir hizmet için verdiği sipariş.
public class Order
{
    public Guid Id { get; set; }

    public Guid StationId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid ServiceId { get; set; }

    // Satış anındaki fiyatın kopyası. Hizmet fiyatı değişse de bu sabit kalır.
    public decimal Amount { get; set; }

    // Sipariş anındaki orana göre hesaplanır; oran sonradan değişse de sabit kalır.
    // İstasyonun hakedişi = Amount - CommissionAmount.
    public decimal CommissionAmount { get; set; }

    // Setter private: kimse doğrudan yazamaz. Değişiklik OrderStateMachine'den geçer.
    public OrderStatus Status { get; private set; }

    public string IdempotencyKey { get; set; } = null!;

    public string? ProviderPaymentId { get; set; }

    // Aynı Idempotency-Key ile gelen tekrar isteğe birebir aynı yanıtı dönebilmek için saklanır.
    public string? CheckoutRedirectUrl { get; set; }

    public string? FailureReason { get; set; }

    // Ödeme gelmezse siparişin düşeceği an.
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public Station Station { get; set; } = null!;

    public ApplicationUser Customer { get; set; } = null!;

    public Service Service { get; set; } = null!;

    public Ticket? Ticket { get; set; }

    // Sadece OrderStateMachine çağırır. Kuralları o doğrular, burası sadece yazar.
    internal void ApplyStatus(OrderStatus next) => Status = next;
}
