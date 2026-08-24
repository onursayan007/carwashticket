namespace CarWashTicket.Api.Entities;

// Identity rol adları. [Authorize(Roles = ...)] sabit metin istediği için const.
public static class Roles
{
    public const string Customer = "Customer";

    // Yıkama noktasında bileti okutan.
    public const string Scanner = "Scanner";

    // İşyeri sahibi: fiyatları belirler, satışlarını görür.
    public const string Business = "Business";

    // Platform yöneticisi. Kiracı izolasyonunun dışındadır.
    public const string Admin = "Admin";

    public static readonly string[] All = [Customer, Scanner, Business, Admin];
}
