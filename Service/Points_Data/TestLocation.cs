using DataAccess.Entity.GrH;

namespace Service.Points_Data;

public class TestLocation
{
    public static IReadOnlyList<LocationPoint> Points { get; } =
        GeneratePoints();

    private static List<LocationPoint> GeneratePoints()
    {
        var points = new List<LocationPoint>(300);

        //Msc
        double baseLat = 55.7558;
        double baseLon = 37.6176;
        
        for (int latStep = 0; latStep < 6; latStep++)
        {
            for (int lonStep = 0; lonStep < 6; lonStep++)
            {
                points.Add(new LocationPoint
                (
                    baseLat + latStep * 0.01,
                    baseLon + lonStep * 0.01
                ));
            }
        }

        return points;
    }
}