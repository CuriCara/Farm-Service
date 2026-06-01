using System.Globalization;
namespace DataAccess.Entity.GrH;

public static class CoordinateExtensions
{
    // Конвертируем точку в стрингу 
    public static string ToGraphHopperString(this LocationPoint point)
    {
        return $"{point.Latitude.ToString(CultureInfo.InvariantCulture)},{point.Longitude.ToString(CultureInfo.InvariantCulture)}";
    }
}