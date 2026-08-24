namespace CarWashTicket.Api.Entities;

// Siparişteki tek kalem: "2 adet su".
public class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ServiceId { get; set; }

    // Satış anındaki ad ve fiyatın kopyası; hizmet sonradan değişse de fiş bozulmaz.
    public string ServiceName { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;

    public Service Service { get; set; } = null!;
}
