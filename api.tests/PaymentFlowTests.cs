using System.Net;
using System.Net.Http.Json;
using CarWashTicket.Api.Data;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashTicket.Api.Tests;

public class PaymentFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly TestWorld _world = new(factory);

    [Fact]
    public async Task Ayni_idempotency_key_ile_iki_istek_tek_siparis_yaratir()
    {
        var (stationId, serviceId) = await _world.CreateStationAsync();
        var customer = await _world.CreateClientAsync(Roles.Customer);
        var key = Guid.NewGuid().ToString();

        var first = await CreateOrderAsync(customer, stationId, serviceId, key);
        var second = await CreateOrderAsync(customer, stationId, serviceId, key);

        Assert.Equal(first.OrderId, second.OrderId);

        using var scope = _world.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await db.Orders.CountAsync(o => o.IdempotencyKey == key));
    }

    [Fact]
    public async Task Ayni_provider_event_id_ile_iki_webhook_tek_kez_islenir()
    {
        var (stationId, serviceId) = await _world.CreateStationAsync();
        var customer = await _world.CreateClientAsync(Roles.Customer);

        var order = await CreateOrderAsync(customer, stationId, serviceId, Guid.NewGuid().ToString());

        var eventId = $"evt_{Guid.NewGuid():N}";

        var first = await SendWebhookAsync(order.OrderId, eventId, 250.00m);
        var second = await SendWebhookAsync(order.OrderId, eventId, 250.00m);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = _world.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await db.WebhookEvents.CountAsync(w => w.ProviderEventId == eventId));

        // Tek kez işlendiğinin asıl kanıtı: ikinci bilet ve ikinci defter seti oluşmamış.
        Assert.Equal(1, await db.Tickets.CountAsync(t => t.OrderId == order.OrderId));
        Assert.Equal(3, await db.LedgerEntries.CountAsync(e => e.OrderId == order.OrderId));
    }

    [Fact]
    public async Task Ayni_bilet_ikinci_kez_okutulamaz()
    {
        var (stationId, serviceId) = await _world.CreateStationAsync();
        var customer = await _world.CreateClientAsync(Roles.Customer);
        var staff = await _world.CreateClientAsync(Roles.Scanner, stationId);

        var order = await CreateOrderAsync(customer, stationId, serviceId, Guid.NewGuid().ToString());
        await SendWebhookAsync(order.OrderId, $"evt_{Guid.NewGuid():N}", 250.00m);

        string code;

        using (var scope = _world.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            code = await db.Tickets.Where(t => t.OrderId == order.OrderId)
                .Select(t => t.Code)
                .SingleAsync();
        }

        var first = await RedeemAsync(staff, code);
        var second = await RedeemAsync(staff, code);

        Assert.True(first.Success);
        Assert.False(second.Success);
    }

    [Fact]
    public async Task Odenmemis_siparis_icin_bilet_uretilmez()
    {
        var (stationId, serviceId) = await _world.CreateStationAsync();
        var customer = await _world.CreateClientAsync(Roles.Customer);

        var order = await CreateOrderAsync(customer, stationId, serviceId, Guid.NewGuid().ToString());

        using var scope = _world.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var status = await db.Orders.Where(o => o.Id == order.OrderId)
            .Select(o => o.Status)
            .SingleAsync();

        Assert.Equal(OrderStatus.AwaitingPayment, status);
        Assert.False(await db.Tickets.AnyAsync(t => t.OrderId == order.OrderId));
    }

    [Fact]
    public async Task Bir_siparisin_defter_kayitlarinin_toplami_sifirdir()
    {
        var (stationId, serviceId) = await _world.CreateStationAsync(price: 333.33m);
        var customer = await _world.CreateClientAsync(Roles.Customer);

        var order = await CreateOrderAsync(customer, stationId, serviceId, Guid.NewGuid().ToString());
        await SendWebhookAsync(order.OrderId, $"evt_{Guid.NewGuid():N}", 333.33m);

        using var scope = _world.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();

        var entries = await db.LedgerEntries
            .Where(e => e.OrderId == order.OrderId)
            .Select(e => new { e.Direction, e.Amount })
            .ToListAsync();

        Assert.NotEmpty(entries);

        var balance = entries.Sum(e => e.Direction == LedgerDirection.Debit ? e.Amount : -e.Amount);

        Assert.Equal(0m, balance);
        Assert.True(await provider.GetRequiredService<LedgerService>()
            .IsOrderBalancedAsync(order.OrderId));
    }

    [Fact]
    public async Task Manager_baska_istasyonun_siparislerini_goremez()
    {
        var (ownStationId, _) = await _world.CreateStationAsync();
        var (otherStationId, otherServiceId) = await _world.CreateStationAsync();

        var manager = await _world.CreateClientAsync(Roles.Business, ownStationId);
        var customer = await _world.CreateClientAsync(Roles.Customer);

        // Diğer istasyonda gerçek bir sipariş var; yönetici onu görememeli.
        await CreateOrderAsync(customer, otherStationId, otherServiceId, Guid.NewGuid().ToString());

        var forbidden = await manager.GetAsync($"/api/panel/orders?stationId={otherStationId}");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Kendi istasyonunu sorduğunda o siparişi listede bulmamalı.
        var own = await manager.GetAsync($"/api/panel/orders?stationId={ownStationId}");

        own.EnsureSuccessStatusCode();

        var orders = await own.Content.ReadFromJsonAsync<List<PanelOrder>>();

        Assert.Empty(orders!);
    }

    // Altı testin dışında: çok kalemli siparişin çekirdek davranışı.
    [Fact]
    public async Task Cok_kalemli_siparis_her_birim_icin_ayri_bilet_uretir()
    {
        var (stationId, suId) = await _world.CreateStationAsync(price: 30.00m);
        var kopukId = await _world.AddServiceAsync(stationId, "Köpük", 45.00m);
        var customer = await _world.CreateClientAsync(Roles.Customer);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                stationId,
                items = new[]
                {
                    new { serviceId = suId, quantity = 2 },
                    new { serviceId = kopukId, quantity = 1 }
                }
            })
        };

        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await customer.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var order = (await response.Content.ReadFromJsonAsync<CreatedOrder>())!;

        // 2 x 30 + 1 x 45
        Assert.Equal(105.00m, order.Amount);

        await SendWebhookAsync(order.OrderId, $"evt_{Guid.NewGuid():N}", 105.00m);

        using var scope = _world.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tickets = await db.Tickets
            .Where(t => t.OrderId == order.OrderId)
            .Select(t => t.ServiceName)
            .ToListAsync();

        Assert.Equal(3, tickets.Count);
        Assert.Equal(2, tickets.Count(n => n == "Su"));
        Assert.Equal(1, tickets.Count(n => n == "Köpük"));

        // Her biletin kodu ayrı olmalı; aksi halde biri diğerini tüketirdi.
        Assert.Equal(3, await db.Tickets
            .Where(t => t.OrderId == order.OrderId)
            .Select(t => t.Code)
            .Distinct()
            .CountAsync());
    }

    private static async Task<CreatedOrder> CreateOrderAsync(
        HttpClient client,
        Guid stationId,
        Guid serviceId,
        string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { stationId, items = new[] { new { serviceId, quantity = 1 } } })
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatedOrder>())!;
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(Guid orderId, string eventId, decimal amount)
    {
        var client = factory.CreateClient();

        return await client.PostAsJsonAsync("/api/payments/webhook", new
        {
            eventId,
            eventType = "payment",
            orderId = orderId.ToString(),
            paymentId = $"pay_{orderId:N}",
            amount = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            status = "success"
        });
    }

    private static async Task<RedeemResponse> RedeemAsync(HttpClient staff, string code)
    {
        var response = await staff.PostAsJsonAsync("/api/tickets/redeem", new { code });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RedeemResponse>())!;
    }

    private record CreatedOrder(Guid OrderId, string Status, decimal Amount, string? RedirectUrl);

    private record RedeemResponse(bool Success, string Message);

    private record PanelOrder(Guid Id, decimal Amount);
}
