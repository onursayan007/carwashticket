using System.Security.Cryptography;
using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Controllers;

// Platform yöneticisi. Kural 8'in tek istisnası: admin tüm istasyonları görür.
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    INotificationSender notifications,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("businesses")]
    [ProducesResponseType<IReadOnlyList<BusinessSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BusinessSummaryDto>>> GetBusinesses(
        CancellationToken ct)
    {
        var businesses = await db.Stations
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new BusinessSummaryDto(
                s.Id,
                s.Name,
                s.Type,
                s.CompanyName,
                s.City,
                s.District,
                s.Latitude,
                s.Longitude,
                s.ContactEmail,
                s.IsActive,
                s.Services.Count,
                s.CreatedAt))
            .ToListAsync(ct);

        return Ok(businesses);
    }

    [HttpPost("businesses")]
    [ProducesResponseType<CreateBusinessResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateBusinessResponse>> CreateBusiness(
        CreateBusinessRequest request,
        CancellationToken ct)
    {
        var email = request.ContactEmail.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Problem(
                detail: "Bu e-posta ile bir kullanıcı zaten var.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!await roleManager.RoleExistsAsync(Roles.Business))
        {
            await roleManager.CreateAsync(new ApplicationRole { Name = Roles.Business });
        }

        var now = DateTimeOffset.UtcNow;

        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Type = request.Type,
            CompanyName = request.CompanyName?.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            TaxOffice = request.TaxOffice?.Trim(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            District = request.District?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ContactEmail = email,
            PhoneNumber = request.PhoneNumber?.Trim(),
            IsActive = true,
            CreatedAt = now
        };

        db.Stations.Add(station);
        await db.SaveChangesAsync(ct);

        var temporaryPassword = GenerateTemporaryPassword();

        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.CompanyName?.Trim() ?? request.Name.Trim(),
            // İlk girişte değiştirmesi gerekiyor.
            MustChangePassword = true,
            CreatedAt = now
        };

        var created = await userManager.CreateAsync(owner, temporaryPassword);

        if (!created.Succeeded)
        {
            // Kullanıcı açılamadıysa istasyonu da bırakmıyoruz.
            db.Stations.Remove(station);
            await db.SaveChangesAsync(ct);

            foreach (var failure in created.Errors)
            {
                ModelState.AddModelError(failure.Code, failure.Description);
            }

            return ValidationProblem(ModelState);
        }

        await userManager.AddToRoleAsync(owner, Roles.Business);

        db.StationStaff.Add(new StationStaff
        {
            StationId = station.Id,
            UserId = owner.Id,
            Role = StationRole.Business,
            AssignedAt = now
        });

        await db.SaveChangesAsync(ct);

        await notifications.SendAsync(
            new NotificationMessage(
                email,
                station.PhoneNumber,
                "Araç yıkama platformu giriş bilgileriniz",
                $"""
                 {station.Name} için işyeri hesabınız açıldı.

                 E-posta: {email}
                 Geçici şifre: {temporaryPassword}

                 İlk girişte şifrenizi değiştirmeniz gerekiyor.
                 """),
            ct);

        logger.LogInformation(
            "İşyeri oluşturuldu. İstasyon {StationId}, sahip {Email}", station.Id, email);

        return CreatedAtAction(
            nameof(GetBusinesses),
            new CreateBusinessResponse(
                station.Id,
                email,
                "İşyeri oluşturuldu, geçici şifre gönderildi."));
    }

    // Identity politikası: en az 8 karakter, büyük/küçük harf ve rakam.
    // Karıştırılabilir karakterler (0/O, 1/l) bilerek dışarıda.
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        var chars = new List<char>
        {
            Pick(upper),
            Pick(lower),
            Pick(digits)
        };

        while (chars.Count < 10)
        {
            chars.Add(Pick(all));
        }

        // Zorunlu karakterlerin hep aynı sırada olmaması için karıştırılıyor.
        return new string([.. chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))]);

        static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
    }
}
