using CarWashTicket.Api.Data;
using CarWashTicket.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CarWashTicket.Api.Stations;

// Haritada ve listede gösterilecek işyerlerini bulur.
// Mesafe PostGIS yerine haversine ile hesaplanıyor: birkaç bin kayıt için
// fazlasıyla yeterli ve ekstra bağımlılık getirmiyor.
public class StationQueryService(AppDbContext db)
{
    private const double EarthRadiusKm = 6371.0;

    public async Task<IReadOnlyList<StationSummaryDto>> SearchAsync(
        double? latitude,
        double? longitude,
        StationSort sort,
        double radiusKm,
        int limit,
        CancellationToken ct = default)
    {
        var query = db.Stations.AsNoTracking().Where(s => s.IsActive);

        // Kaba kutu filtresi veritabanında; kesin mesafe bellekte.
        // Kutu, index'ten faydalanıp taranacak satır sayısını düşürüyor.
        if (latitude.HasValue && longitude.HasValue)
        {
            var latDelta = radiusKm / 111.0;
            var lngDelta = radiusKm / (111.0 * Math.Max(0.01, Math.Cos(ToRadians(latitude.Value))));

            query = query.Where(s =>
                s.Latitude >= latitude.Value - latDelta
                && s.Latitude <= latitude.Value + latDelta
                && s.Longitude >= longitude.Value - lngDelta
                && s.Longitude <= longitude.Value + lngDelta);
        }

        var rows = await query
            .Select(s => new StationSummaryDto(
                s.Id,
                s.Name,
                s.Type,
                s.Address,
                s.City,
                s.District,
                s.Latitude,
                s.Longitude,
                s.RatingAverage,
                s.RatingCount,
                s.Services.Where(x => x.IsActive).Min(x => (decimal?)x.Price),
                null))
            .ToListAsync(ct);

        var results = rows;

        if (latitude.HasValue && longitude.HasValue)
        {
            results = rows
                .Select(s => s with
                {
                    DistanceKm = Distance(latitude.Value, longitude.Value, s.Latitude, s.Longitude)
                })
                .Where(s => s.DistanceKm <= radiusKm)
                .ToList();
        }

        return Sort(results, sort).Take(limit).ToList();
    }

    private static IEnumerable<StationSummaryDto> Sort(
        List<StationSummaryDto> stations,
        StationSort sort) => sort switch
    {
        // Konum yoksa mesafe null; onları sona atıyoruz.
        StationSort.Nearest => stations.OrderBy(s => s.DistanceKm ?? double.MaxValue),

        // Hiç hizmeti olmayan işyeri "en ucuz" listesinin başına geçmesin.
        StationSort.Cheapest => stations.OrderBy(s => s.MinPrice ?? decimal.MaxValue),

        StationSort.TopRated => stations
            .OrderByDescending(s => s.RatingAverage)
            .ThenByDescending(s => s.RatingCount),

        _ => stations.OrderByDescending(BestScore)
    };

    // "En iyi seçim" ilk sürüm: puan yarı ağırlıklı, yakınlık ve fiyat tamamlayıcı.
    // Formül tartışmaya açık, tek yerde durduğu için değiştirmesi kolay.
    private static double BestScore(StationSummaryDto station)
    {
        var rating = (double)station.RatingAverage / 5.0;

        // 0 km'de 1, 25 km'de 0.
        var proximity = station.DistanceKm.HasValue
            ? Math.Max(0, 1 - station.DistanceKm.Value / 25.0)
            : 0.5;

        // 100 TL'de ~0.5, ucuzladıkça 1'e yaklaşır.
        var price = station.MinPrice.HasValue && station.MinPrice.Value > 0
            ? 100.0 / (100.0 + (double)station.MinPrice.Value)
            : 0.3;

        return (rating * 0.5) + (proximity * 0.3) + (price * 0.2);
    }

    private static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
