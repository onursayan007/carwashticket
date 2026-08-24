using CarWashTicket.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CarWashTicket.Api.Tests;

// Her koşuda kendi veritabanını yaratır, sonunda siler. InMemory yerine gerçek
// Postgres kullanıyoruz: unique index ve ExecuteUpdate davranışları InMemory'de
// doğrulanamaz, oysa test edilen davranışların yarısı tam olarak onlara dayanıyor.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"carwashticket_test_{Guid.NewGuid():N}";

    public ApiFactory()
    {
        // Program.cs yapılandırmayı Build()'den ÖNCE okuyor; ConfigureAppConfiguration
        // o noktada henüz uygulanmamış oluyor. Ortam değişkenleri ise
        // WebApplicationBuilder tarafından baştan okunuyor.
        var settings = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["ConnectionStrings__Postgres"] = TestConnectionString,
            ["Jwt__Key"] = "test-only-signing-key-at-least-32-bytes-long!!",
            ["Jwt__Issuer"] = "carwashticket",
            ["Jwt__Audience"] = "carwashticket-web",
            // Testing ortamında varsayılan gerçek sağlayıcı olurdu; açıkça mock diyoruz.
            ["Payment__UseMock"] = "true",
            ["Payment__CommissionRate"] = "0.10",
            ["Payment__CallbackUrl"] = "https://localhost/api/payments/callback",
            ["Spa__BaseUrl"] = "https://localhost:5173"
        };

        foreach (var (key, value) in settings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    // Varsayılan, yerel geliştirme Postgres'i. Buradaki şifre sadece bu makinedeki
    // test veritabanı için; başka ortamda TEST_POSTGRES ile ezilir.
    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=carwash_dev;Database=postgres";

    private string TestConnectionString =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = _databaseName }
            .ConnectionString;

    public async Task InitializeAsync()
    {
        await using (var connection = new NpgsqlConnection(AdminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        // Şema, üretimdekiyle aynı migration'lardan kuruluyor.
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    // Development değil: DevDataSeeder çalışmasın, testler kendi verisini kursun.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseEnvironment("Testing");
}
