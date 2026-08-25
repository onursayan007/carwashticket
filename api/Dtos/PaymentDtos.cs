using System.ComponentModel.DataAnnotations;

namespace CarWashTicket.Api.Dtos;

// Sahte 3DS ekranından gelen sonuç. Kart bilgisi TAŞIMAZ ve saklanmaz;
// sadece kullanıcının onayladığı/reddettiği bilgisi gelir.
public record MockCallbackRequest
{
    [Required, MaxLength(128)]
    public string ProviderRef { get; init; } = null!;

    [Required]
    public string Outcome { get; init; } = null!;
}

public record MockCallbackResponse(Guid OrderId, string Status);
