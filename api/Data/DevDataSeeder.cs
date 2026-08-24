using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Data;

// Geliştirme ortamı örnek verisi. Veritabanında istasyon varsa hiçbir şey yapmaz.
public static class DevDataSeeder
{
    private const string DemoPassword = "Demo123!";

    private static readonly string[] Roles = ["Customer", "Staff", "Manager"];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (await db.Stations.AnyAsync())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
        }

        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = "Merkez Petrol - Kadıköy",
            Address = "Bağdat Caddesi No:120, Kadıköy / İstanbul",
            PhoneNumber = "02165550101",
            IsActive = true,
            CreatedAt = now
        };

        db.Stations.Add(station);

        db.Services.AddRange(
            new Service
            {
                Id = Guid.NewGuid(),
                StationId = station.Id,
                Name = "Hızlı Dış Yıkama",
                Description = "Sadece dış gövde köpük ve durulama.",
                Price = 250.00m,
                DurationMinutes = 15,
                IsActive = true,
                CreatedAt = now
            },
            new Service
            {
                Id = Guid.NewGuid(),
                StationId = station.Id,
                Name = "Dış Yıkama + Kurulama",
                Description = "Dış yıkama, el ile kurulama ve jant temizliği.",
                Price = 400.00m,
                DurationMinutes = 25,
                IsActive = true,
                CreatedAt = now
            },
            new Service
            {
                Id = Guid.NewGuid(),
                StationId = station.Id,
                Name = "İç Dış Detaylı Temizlik",
                Description = "Dış yıkama, iç süpürme, torpido ve cam temizliği.",
                Price = 750.00m,
                DurationMinutes = 50,
                IsActive = true,
                CreatedAt = now
            },
            new Service
            {
                Id = Guid.NewGuid(),
                StationId = station.Id,
                Name = "Cilalı Full Bakım",
                Description = "İç dış detaylı temizlik, cila ve koltuk şampuanlama.",
                Price = 1200.00m,
                DurationMinutes = 90,
                IsActive = true,
                CreatedAt = now
            });

        await db.SaveChangesAsync();

        // UserManager kendi kaydını yapar, bu yüzden istasyondan sonra çağrılıyor.
        await CreateUserAsync(userManager, "demo@test.com", "Demo Müşteri", "Customer", now);
        var staff = await CreateUserAsync(userManager, "staff@test.com", "Yıkama Personeli", "Staff", now);
        var manager = await CreateUserAsync(userManager, "manager@test.com", "İstasyon Müdürü", "Manager", now);

        db.StationStaff.AddRange(
            new StationStaff
            {
                StationId = station.Id,
                UserId = staff.Id,
                Role = StationRole.Staff,
                AssignedAt = now
            },
            new StationStaff
            {
                StationId = station.Id,
                UserId = manager.Id,
                Role = StationRole.Manager,
                AssignedAt = now
            });

        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        DateTimeOffset now)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CreatedAt = now
        };

        var result = await userManager.CreateAsync(user, DemoPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"{email} oluşturulamadı: {errors}");
        }

        await userManager.AddToRoleAsync(user, role);

        return user;
    }
}
