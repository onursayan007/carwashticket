using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Data;

// Geliştirme ve demo verisi. Veritabanında istasyon varsa hiçbir şey yapmaz.
public static class DevDataSeeder
{
    private const string DemoPassword = "Demo123!";

    // İşyeri paneli tek istasyon bekliyor; demo işyeri sahibi buraya bağlı.
    private const string DemoBusinessStation = "Petrol Ofisi Karyağdı";

    // QR okuyucu birden fazla istasyonda görevli olabilir. Demoda üç ilçeye
    // yayılmış istasyonlara bağlıyoruz ki nereye bakarsan bak bilet okutulabilsin.
    private static readonly string[] DemoScannerStations =
    [
        "Petrol Ofisi Karyağdı",
        "Opet Elmalı",
        "Opet (Sakarya Blv.)",
        "Petrol Ofisi (Sakarya Blv.)",
        "Shell (Sakarya Blv.)",
        "Petrol Ofisi (Fevzi Çakmak)",
        "Opet (Etiler)",
        "Petrol Ofisi (Meydankavağı)",
        "Total (Etiler)"
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (await db.Stations.AnyAsync())
        {
            // Veri zaten var: sadece eksik kalan demo atamalarını tamamla.
            // Seed listesi büyüdüğünde veritabanını sıfırlamak gerekmesin.
            await EnsureScannerAssignmentsAsync(db, userManager);
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

        var businessStation = stations.First(s => s.Name == DemoBusinessStation);

        db.StationStaff.Add(new StationStaff
        {
            StationId = businessStation.Id,
            UserId = business.Id,
            Role = StationRole.Business,
            AssignedAt = now
        });

        db.StationStaff.AddRange(
            stations
                .Where(s => DemoScannerStations.Contains(s.Name))
                .Select(s => new StationStaff
                {
                    StationId = s.Id,
                    UserId = staff.Id,
                    Role = StationRole.Scanner,
                    AssignedAt = now
                }));

        await db.SaveChangesAsync();
    }

    // Demo QR okuyucusunun görevli olduğu istasyonları tamamlar. Var olanı
    // tekrar eklemez, bu yüzden her açılışta güvenle çalışabilir.
    private static async Task EnsureScannerAssignmentsAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        var scanner = await userManager.FindByEmailAsync("staff@test.com");

        if (scanner is null)
        {
            return;
        }

        var targetIds = await db.Stations
            .Where(s => DemoScannerStations.Contains(s.Name))
            .Select(s => s.Id)
            .ToListAsync();

        var existing = await db.StationStaff
            .Where(ss => ss.UserId == scanner.Id)
            .Select(ss => ss.StationId)
            .ToListAsync();

        var missing = targetIds.Except(existing).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.StationStaff.AddRange(missing.Select(id => new StationStaff
        {
            StationId = id,
            UserId = scanner.Id,
            Role = StationRole.Scanner,
            AssignedAt = DateTimeOffset.UtcNow
        }));

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
