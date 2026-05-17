namespace GestorCampo.Application.Common;

public class GeofenceService
{
    private const double EarthRadiusMeters = 6_371_000;

    public (bool isWithin, int distanceMeters) Compute(
        double aLat, double aLng, double bLat, double bLng, int thresholdMeters)
    {
        var dLat = ToRadians(bLat - aLat);
        var dLng = ToRadians(bLng - aLng);
        var lat1 = ToRadians(aLat);
        var lat2 = ToRadians(bLat);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        var distance = (int)Math.Round(EarthRadiusMeters * c);
        return (distance <= thresholdMeters, distance);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
