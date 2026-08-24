using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Orders;

// Sipariş durumunu değiştirmenin tek yolu. Order.Status'un setter'ı private,
// yazma işini yapan Order.ApplyStatus internal ve sadece buradan çağrılır.
public class OrderStateMachine(ILogger<OrderStateMachine> logger)
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Created] = [OrderStatus.AwaitingPayment, OrderStatus.Failed, OrderStatus.Expired],
        [OrderStatus.AwaitingPayment] = [OrderStatus.Paid, OrderStatus.Failed, OrderStatus.Expired],
        [OrderStatus.Paid] = [OrderStatus.Redeemed, OrderStatus.Refunded],
        [OrderStatus.Redeemed] = [OrderStatus.Settled, OrderStatus.Refunded],
        [OrderStatus.Settled] = [OrderStatus.Refunded],

        // Bitiş durumları: buradan çıkış yok, iade için yeni sipariş açılır.
        [OrderStatus.Failed] = [],
        [OrderStatus.Expired] = [],
        [OrderStatus.Refunded] = []
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<OrderStatus> AllowedFrom(OrderStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : [];

    public void Transition(Order order, OrderStatus next)
    {
        var current = order.Status;

        if (!CanTransition(current, next))
        {
            logger.LogWarning(
                "Geçersiz durum geçişi reddedildi. Sipariş {OrderId}: {From} -> {To}",
                order.Id, current, next);

            throw new InvalidOrderTransitionException(order.Id, current, next);
        }

        order.ApplyStatus(next);

        if (next == OrderStatus.Paid)
        {
            order.PaidAt = DateTimeOffset.UtcNow;
        }

        logger.LogInformation(
            "Sipariş durumu değişti. Sipariş {OrderId}: {From} -> {To}",
            order.Id, current, next);
    }
}
