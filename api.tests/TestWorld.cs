using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarWashTicket.Api.Data;
using CarWashTicket.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashTicket.Api.Tests;

// Testlerin ihtiyaç duyduğu kurulum: istasyon, hizmet, kullanıcı, giriş.
public class TestWorld(ApiFactory factory)
{
    private const string Password = "Test123!";

    public async Task<(Guid StationId, Guid ServiceId)> CreateStationAsync(decimal price = 250.00m)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = $"İstasyon {Guid.NewGuid():N}"[..20],
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var service = new Service
        {
            Id = Guid.NewGuid(),
            StationId = station.Id,
            Name = "Dış Yıkama",
            Price = price,
            DurationMinutes = 20,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Stations.Add(station);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        return (station.Id, service.Id);
    }

    // Rolü verilen bir kullanıcı yaratır, istasyon verilirse oraya atar,
    // giriş yapıp yetkili bir HttpClient döner.
    public async Task<HttpClient> CreateClientAsync(string role, Guid? stationId = null)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";

        using (var scope = factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = provider.GetRequiredService<RoleManager<ApplicationRole>>();
            var db = provider.GetRequiredService<AppDbContext>();

            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new ApplicationRole { Name = role });
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var created = await users.CreateAsync(user, Password);

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", created.Errors.Select(e => e.Description)));
            }

            await users.AddToRoleAsync(user, role);

            if (stationId.HasValue)
            {
                db.StationStaff.Add(new StationStaff
                {
                    StationId = stationId.Value,
                    UserId = user.Id,
                    Role = role == "Manager" ? StationRole.Manager : StationRole.Staff,
                    AssignedAt = DateTimeOffset.UtcNow
                });

                await db.SaveChangesAsync();
            }
        }

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<LoginResult>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    public IServiceScope CreateScope() => factory.Services.CreateScope();

    private record LoginResult(string AccessToken);
}
