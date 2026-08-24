using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<StationStaff> StationStaff => Set<StationStaff>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200);
        });

        builder.Entity<Station>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.District).HasMaxLength(100);
            e.Property(x => x.CompanyName).HasMaxLength(300);
            e.Property(x => x.TaxNumber).HasMaxLength(20);
            e.Property(x => x.TaxOffice).HasMaxLength(150);
            e.Property(x => x.ContactEmail).HasMaxLength(256);
            e.Property(x => x.PhoneNumber).HasMaxLength(30);
            e.Property(x => x.RatingAverage).HasPrecision(3, 2);

            // Haritada görünen işyerlerini bir kutu içinde çekmek için.
            e.HasIndex(x => new { x.IsActive, x.Latitude, x.Longitude });
        });

        builder.Entity<StationStaff>(e =>
        {
            e.HasKey(x => new { x.StationId, x.UserId });

            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);

            e.HasOne(x => x.Station)
                .WithMany(s => s.Staff)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany(u => u.StationAssignments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.UserId);
        });

        builder.Entity<Service>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Price).HasPrecision(18, 2);

            e.HasOne(x => x.Station)
                .WithMany(s => s.Services)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.StationId, x.IsActive });
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ServiceName).IsRequired().HasMaxLength(200);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);

            e.HasOne(x => x.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.OrderId);
        });

        builder.Entity<Review>(e =>
        {
            e.Property(x => x.Comment).HasMaxLength(1000);

            // Sipariş başına tek değerlendirme.
            e.HasIndex(x => x.OrderId).IsUnique();

            e.HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.StationId);
        });

        builder.Entity<Order>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.CommissionAmount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(64);
            e.Property(x => x.ProviderPaymentId).HasMaxLength(128);
            e.Property(x => x.CheckoutRedirectUrl).HasMaxLength(2048);
            e.Property(x => x.FailureReason).HasMaxLength(500);

            // Aynı istek iki kez sipariş oluşturamaz.
            e.HasIndex(x => x.IdempotencyKey).IsUnique();

            e.HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.StationId, x.Status });
            e.HasIndex(x => new { x.StationId, x.CreatedAt });
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.ProviderPaymentId);
        });

        builder.Entity<Ticket>(e =>
        {
            e.Property(x => x.Code).IsRequired().HasMaxLength(64);
            e.Property(x => x.ServiceName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

            // QR kodu sistem genelinde tekil.
            e.HasIndex(x => x.Code).IsUnique();

            // Bir siparişte satın alınan her birim için ayrı bilet.
            e.HasOne(x => x.Order)
                .WithMany(o => o.Tickets)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.OrderId);

            e.HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RedeemedByUser)
                .WithMany()
                .HasForeignKey(x => x.RedeemedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.StationId, x.Status });
        });

        builder.Entity<LedgerEntry>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Account).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Description).HasMaxLength(500);

            e.HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.TransactionId);
            e.HasIndex(x => new { x.StationId, x.CreatedAt });
            e.HasIndex(x => x.OrderId);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);

            e.HasIndex(x => x.TokenHash).IsUnique();

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.UserId);
        });

        builder.Entity<WebhookEvent>(e =>
        {
            e.Property(x => x.Provider).IsRequired().HasMaxLength(32);
            e.Property(x => x.ProviderEventId).IsRequired().HasMaxLength(128);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(64);
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Error).HasMaxLength(1000);

            // Aynı olay ikinci kez işlenmez.
            e.HasIndex(x => x.ProviderEventId).IsUnique();

            e.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.OrderId);
        });
    }
}
