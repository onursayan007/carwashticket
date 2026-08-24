using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Data;

// Geliştirme ortamı örnek verisi. Veritabanında istasyon varsa hiçbir şey yapmaz.
public static class DevDataSeeder
{
    private const string DemoPassword = "Demo123!";

    private static Service Unit(
        Guid stationId, string name, string description, decimal price, int minutes, DateTimeOffset now)
        => Build(stationId, name, description, ServiceKind.Unit, price, minutes, now);

    private static Service Package(
        Guid stationId, string name, string description, decimal price, int minutes, DateTimeOffset now)
        => Build(stationId, name, description, ServiceKind.Package, price, minutes, now);

    private static Service Build(
        Guid stationId,
        string name,
        string description,
        ServiceKind kind,
        decimal price,
        int minutes,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            Name = name,
            Description = description,
            Kind = kind,
            Price = price,
            DurationMinutes = minutes,
            IsActive = true,
            CreatedAt = now
        };

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

        foreach (var role in Entities.Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
        }

        // Self servis: birim satılır, müşteri kendi yıkar.
        var selfService = new Station
        {
            Id = Guid.NewGuid(),
            Name = "Elmalı Petrol Self Servis",
            Type = StationType.SelfService,
            Address = "Zafer Mahallesi, Antalya Caddesi No:42",
            City = "Antalya",
            District = "Elmalı",
            Latitude = 36.7387,
            Longitude = 29.9075,
            CompanyName = "Elmalı Akaryakıt Ltd. Şti.",
            TaxNumber = "1234567890",
            TaxOffice = "Elmalı",
            ContactEmail = "isyeri@test.com",
            PhoneNumber = "02428110101",
            RatingAverage = 4.30m,
            RatingCount = 27,
            IsActive = true,
            CreatedAt = now
        };

        // Tam hizmet: paket satılır, personel aracı teslim alır.
        var fullService = new Station
        {
            Id = Guid.NewGuid(),
            Name = "Lara Oto Kuaför",
            Type = StationType.FullService,
            Address = "Fener Mahallesi, Lara Caddesi No:7",
            City = "Antalya",
            District = "Muratpaşa",
            Latitude = 36.8529,
            Longitude = 30.7610,
            CompanyName = "Lara Oto Bakım A.Ş.",
            TaxNumber = "9876543210",
            TaxOffice = "Muratpaşa",
            ContactEmail = "lara@test.com",
            PhoneNumber = "02423330202",
            RatingAverage = 4.80m,
            RatingCount = 112,
            IsActive = true,
            CreatedAt = now
        };

        db.Stations.AddRange(selfService, fullService);

        db.Services.AddRange(
            Unit(selfService.Id, "Su", "Yüksek basınçlı su, 1 kullanım.", 30.00m, 4, now),
            Unit(selfService.Id, "Köpük", "Aktif köpük, 1 kullanım.", 45.00m, 4, now),
            Unit(selfService.Id, "Cila", "Sıvı cila, 1 kullanım.", 60.00m, 3, now),
            Unit(selfService.Id, "Süpürge", "Elektrikli süpürge, 1 kullanım.", 25.00m, 5, now),

            Package(fullService.Id, "Dış Yıkama", "Dış gövde köpük, durulama, kurulama.", 400.00m, 25, now),
            Package(fullService.Id, "İç Dış Detaylı", "Dış yıkama, iç süpürme, torpido ve cam.", 750.00m, 50, now),
            Package(fullService.Id, "Cilalı Full Bakım", "Detaylı temizlik, cila, koltuk şampuanlama.", 1200.00m, 90, now));

        var station = selfService;

        await db.SaveChangesAsync();

        // UserManager kendi kaydını yapar, bu yüzden istasyondan sonra çağrılıyor.
        await CreateUserAsync(userManager, "demo@test.com", "Demo Müşteri", Entities.Roles.Customer, now);
        var staff = await CreateUserAsync(userManager, "staff@test.com", "Yıkama Personeli", Entities.Roles.Scanner, now);
        var business = await CreateUserAsync(userManager, "isyeri@test.com", "İşyeri Sahibi", Entities.Roles.Business, now);
        await CreateUserAsync(userManager, "admin@test.com", "Platform Yöneticisi", Entities.Roles.Admin, now);

        db.StationStaff.AddRange(
            new StationStaff
            {
                StationId = station.Id,
                UserId = staff.Id,
                Role = StationRole.Scanner,
                AssignedAt = now
            },
            new StationStaff
            {
                StationId = station.Id,
                UserId = business.Id,
                Role = StationRole.Business,
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
