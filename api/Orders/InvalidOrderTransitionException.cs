using CarWashTicket.Api.Entities;

namespace CarWashTicket.Api.Orders;

public class InvalidOrderTransitionException(Guid orderId, OrderStatus from, OrderStatus to)
    : InvalidOperationException($"Sipariş {orderId} için geçersiz durum geçişi: {from} -> {to}.")
{
    public Guid OrderId { get; } = orderId;

    public OrderStatus From { get; } = from;

    public OrderStatus To { get; } = to;
}
