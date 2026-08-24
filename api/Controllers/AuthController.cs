using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using CarWashTicket.Api.Entities;
using CarWashTicket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    TokenService tokenService) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        // Kayıt olan herkes müşteridir; personel ve müdür atamasını manager yapar.
        await userManager.AddToRoleAsync(user, "Customer");

        return await IssueTokensAsync(user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Kullanıcı yok mu şifre mi yanlış, ayırt edilmiyor.
            return Unauthorized();
        }

        return await IssueTokensAsync(user);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken)
            || string.IsNullOrWhiteSpace(rawToken))
        {
            return Unauthorized();
        }

        var hash = TokenService.Hash(rawToken);
        var now = DateTimeOffset.UtcNow;

        // Tek sorguda geçerlilik kontrolü; süresi dolmuş veya iptal edilmiş kabul edilmez.
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == hash
                && t.RevokedAt == null
                && t.ExpiresAt > now);

        if (stored is null)
        {
            Response.Cookies.Delete(RefreshCookieName);
            return Unauthorized();
        }

        // Rotasyon: eski token bir daha kullanılamaz.
        stored.RevokedAt = now;

        return await IssueTokensAsync(stored.User);
    }

    private async Task<ActionResult<AuthResponse>> IssueTokensAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user, roles);

        var rawRefreshToken = TokenService.CreateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(tokenService.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenService.Hash(rawRefreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        Response.Cookies.Append(RefreshCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = refreshExpiresAt,
            Path = "/api/auth"
        });

        return Ok(new AuthResponse(
            accessToken,
            expiresAt,
            new UserDto(user.Id, user.Email!, user.FullName, [.. roles])));
    }
}
