using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Data;

// Geliştirme ve demo verisi. Veritabanında istasyon varsa hiçbir şey yapmaz.
public static class DevDataSeeder
{
    private const string DemoPassword = "Demo123!";

    // İşyeri ve personel demo hesabının bağlandığı istasyon.
    private const string DemoStationName = "Petrol Ofisi Karyağdı";

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

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
        }

        var stations = new List<Station>();
        var allServices = new List<Service>();

        foreach (var seed in SeedStations.All)
        {
            var station = new Station
            {
                Id = Guid.NewGuid(),
                Name = seed.Name,
                Type = seed.Flavor.ToStationType(),
                Address = seed.Address,
                City = seed.City,
                District = seed.District,
                Latitude = seed.Latitude,
                Longitude = seed.Longitude,
                CompanyName = $"{seed.Name} İşletmesi",
                PhoneNumber = seed.Phone,
                RatingAverage = seed.Rating,
                // Puanla tutarlı, işyerine göre sabit bir yorum sayısı.
                RatingCount = 8 + Spread(seed.Name, 140),
                IsActive = true,
                CreatedAt = now
            };

            stations.Add(station);
            allServices.AddRange(BuildServices(seed, station.Id, now));
        }

        db.Stations.AddRange(stations);
        db.Services.AddRange(allServices);
        await db.SaveChangesAsync();

        // UserManager kendi kaydını yapar, bu yüzden istasyonlardan sonra çağrılıyor.
        await CreateUserAsync(userManager, "demo@test.com", "Demo Müşteri", Roles.Customer, now);
        var staff = await CreateUserAsync(userManager, "staff@test.com", "Yıkama Personeli", Roles.Scanner, now);
        var business = await CreateUserAsync(userManager, "isyeri@test.com", "İşyeri Sahibi", Roles.Business, now);
        await CreateUserAsync(userManager, "admin@test.com", "Platform Yöneticisi", Roles.Admin, now);

        // Panel tek istasyon bekliyor; demo hesapları tek işyerine bağlı.
        var demoStation = stations.First(s => s.Name == DemoStationName);

        db.StationStaff.AddRange(
            new StationStaff
            {
                StationId = demoStation.Id,
                UserId = staff.Id,
                Role = StationRole.Scanner,
                AssignedAt = now
            },
            new StationStaff
            {
                StationId = demoStation.Id,
                UserId = business.Id,
                Role = StationRole.Business,
                AssignedAt = now
            });

        await db.SaveChangesAsync();
    }

    // Fiyatlar işyerine göre ±%20 oynuyor ki "en ucuz" sıralaması anlamlı olsun.
    private static IEnumerable<Service> BuildServices(StationSeed seed, Guid stationId, DateTimeOffset now)
    {
        var factor = 0.8 + Spread(seed.Name, 41) / 100.0;

        decimal Price(int basePrice) => Math.Round((decimal)(basePrice * factor) / 5, 0) * 5;

        var units = new (string Name, string Description, int Price, int Minutes)[]
        {
            ("Su", "Yüksek basınçlı su, 1 kullanım.", 30, 4),
            ("Köpük", "Aktif köpük, 1 kullanım.", 45, 4),
            ("Fırça", "Yumuşak fırça, 1 kullanım.", 25, 3),
            ("Cila", "Sıvı cila, 1 kullanım.", 60, 3),
            ("Süpürge", "Elektrikli süpürge, 1 kullanım.", 25, 5)
        };

        var programs = new (string Name, string Description, int Price, int Minutes)[]
        {
            ("Hızlı Program", "Temassız köpük ve durulama.", 150, 4),
            ("Standart Program", "Köpük, fırça, durulama, kurutma.", 220, 7),
            ("Cilalı Program", "Standart program üzerine sıvı cila.", 300, 10)
        };

        var packages = new List<(string Name, string Description, int Price, int Minutes)>
        {
            ("Dış Yıkama", "Dış gövde köpük, durulama, kurulama.", 300, 25),
            ("İç-Dış Temizlik", "Dış yıkama, iç süpürme, torpido ve cam.", 550, 45)
        };

        // Adında detailing/kuaför geçen yerler daha kapsamlı hizmet veriyor.
        if (seed.Name.Contains("Detailing", StringComparison.OrdinalIgnoreCase)
            || seed.Name.Contains("Kuaför", StringComparison.OrdinalIgnoreCase)
            || seed.Name.Contains("Garage", StringComparison.OrdinalIgnoreCase))
        {
            packages.Add(("Pasta Cila", "Makine ile pasta, cila ve koruma.", 1400, 120));
            packages.Add(("Detailing", "Detaylı iç-dış temizlik, seramik koruma.", 2400, 240));
        }

        if (Spread(seed.Name, 2) == 0)
        {
            packages.Add(("Motor Yıkama", "Motor bölmesi köpükle temizlik.", 350, 30));
        }

        var selected = seed.Flavor switch
        {
            SeedFlavor.Jetonlu => units.Select(u => (u, ServiceKind.Unit)),
            SeedFlavor.Robotik => programs.Select(p => (p, ServiceKind.Unit)),
            SeedFlavor.ElleYikama => packages.Select(p => (p, ServiceKind.Package)),
            _ => units.Select(u => (u, ServiceKind.Unit))
                .Concat(packages.Select(p => (p, ServiceKind.Package)))
        };

        return selected.Select(entry => new Service
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            Name = entry.Item1.Name,
            Description = entry.Item1.Description,
            Kind = entry.Item2,
            Price = Price(entry.Item1.Price),
            DurationMinutes = entry.Item1.Minutes,
            IsActive = true,
            CreatedAt = now
        });
    }

    // Ada bağlı, koşudan koşuya değişmeyen sayı. Random kullanmıyoruz ki
    // aynı seed her ortamda aynı veriyi üretsin.
    private static int Spread(string name, int modulo)
    {
        var hash = name.Aggregate(17, (acc, c) => acc * 31 + c);

        return Math.Abs(hash) % modulo;
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
