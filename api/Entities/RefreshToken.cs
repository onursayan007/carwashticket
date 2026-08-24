namespace CarWashTicket.Api.Entities;

// Refresh token'ın kendisi cookie'de, burada sadece SHA-256 özeti tutulur.
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Kullanıldığında veya çıkış yapıldığında dolar.
    public DateTimeOffset? RevokedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
