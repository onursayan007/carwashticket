using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarWashTicket.Api.Entities;
using Microsoft.IdentityModel.Tokens;

namespace CarWashTicket.Api.Services;

public class TokenService(IConfiguration configuration)
{
    private readonly IConfigurationSection _jwt = configuration.GetSection("Jwt");

    public int RefreshTokenDays => _jwt.GetValue("RefreshTokenDays", 7);

    private int AccessTokenMinutes => _jwt.GetValue("AccessTokenMinutes", 15);

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt["Key"]!));

        var token = new JwtSecurityToken(
            issuer: _jwt["Issuer"],
            audience: _jwt["Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    // Ham token cookie'ye yazılır, özeti veritabanına.
    public static string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
