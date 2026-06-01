using DataAccess.Entity.GrH;

namespace BusinessLogic.GraphHopper.DistanceMatrix;

public interface IDistanceMatrixProvider
{
    Task<DistanceMatrixResult> GetDistanceMatrixAsync(IReadOnlyList<LocationPoint> points);
    Task<(double dist, double time)> GetDistanceAsync(LocationPoint from, LocationPoint to);
    double GetDistanceWithCache(LocationPoint from, LocationPoint to);
    Task PreloadAsync(List<LocationPoint> points);
    DistanceMatrixResult BuildMatrixFromCache(IReadOnlyList<LocationPoint> points);
    Task EnsureCacheReady(IDistanceCache cache, IDistanceMatrixProvider matrixProvider, List<LocationPoint> points);
    Task<List<LocationPoint>> GetStreetGeometryAsync(LocationPoint from, LocationPoint to);
}