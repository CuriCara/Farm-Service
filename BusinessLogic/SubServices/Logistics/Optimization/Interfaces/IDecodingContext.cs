using BusinessLogic.GraphHopper.DistanceMatrix;
using DataAccess;
using DataAccess.Entity;
using DataAccess.Entity.GrH;

namespace BusinessLogic.SubServices.Logistics;

public interface IDecodingContext
{
    IDistanceCache Cache { get; }
    IDistanceMatrixProvider DistanceMatrix { get; }
    List<VehicleInfo> AvailableVehicles { get; }
    Dictionary<(int farmIndex, int productId), double> GetFarmStocksCache();
    void UpdateFarmStocks(Dictionary<(int, int), double> newStocks);
    Dictionary<int, LocationPoint> GetFarmLocations();
    TimeSpan WorkingDayStart { get; }
    TimeSpan WorkingDayEnd { get; }
    double DefaultServiceDurationMinutes { get; }
    DateOnly PlanningDate { get; }
    double TimeWindowPenalty { get; }
    double ShortagePenaltyPerUnit { get; }
    LocationPoint DepotLocation { get; }
    public void Initialize(
        FarmDbContext db,
        List<LocationPoint> allLocations,
        List<Vehicle> vehicles,
        DateOnly planningDate,
        TimeSpan? workingDayStart = null,
        TimeSpan? workingDayEnd = null,
        double defaultServiceDurationMinutes = 15,
        double timeWindowPenalty = 50.0,
        double shortagePenaltyPerUnit = 10.0,
        double costPerKm = 10.0);
}