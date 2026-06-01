using BusinessLogic.GraphHopper.DistanceMatrix.Cahce;

namespace DataAccess.Entity.GrH;

public interface IDistanceCache
{
    bool TryGet(int from, int to, out DistanceTime value);
    void Set(int from, int to, double dist, double time);
    void ClearCache();
    bool ExistsInMemory(int from, int to);
    Task<int> LoadAllFromRedisToMemoryAsync(List<LocationPoint> points);
    bool MemoryOnlyMode { get; set; }
}