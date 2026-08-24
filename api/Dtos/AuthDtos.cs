using System.ComponentModel.DataAnnotations;

namespace CarWashTicket.Api.Dtos;

public record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = null!;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = null!;

    [MaxLength(200)]
    public string? FullName { get; init; }
}

public record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = null!;

    [Required, MaxLength(128)]
    public string Password { get; init; } = null!;
}

public record UserDto(Guid Id, string Email, string? FullName, IReadOnlyList<string> Roles);

// Refresh token gövdede dönmez, httpOnly cookie'ye yazılır.
public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
