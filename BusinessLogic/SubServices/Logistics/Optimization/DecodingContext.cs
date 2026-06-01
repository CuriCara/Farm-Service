using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.SubServices.Logistics;
using BusinessLogic.SubServices.Logistics.DTO;
using DataAccess;
using DataAccess.Entity;
using DataAccess.Entity.GrH;
using Microsoft.EntityFrameworkCore;

public class DecodingContext : IDecodingContext
{
    private Dictionary<(int, int), double> _farmStocksCache { get; set; }
    private Dictionary<int, LocationPoint> _farmLocations { get; set; }
    public IDistanceMatrixProvider DistanceMatrix { get; }
    public IDistanceCache Cache { get; }
    public List<VehicleInfo> AvailableVehicles { get; private set; }
    public LocationPoint DepotLocation { get; private set; }
    public TimeSpan WorkingDayStart { get; private set; }
    public TimeSpan WorkingDayEnd { get; private set; }
    public double DefaultServiceDurationMinutes { get; private set; }
    public DateOnly PlanningDate { get; private set; }
    public double TimeWindowPenalty { get; private set; }
    public double ShortagePenaltyPerUnit { get; private set; }
    public double CostPerKm { get; private set; }

    // Контекст для работы декодера хромосом
    public DecodingContext(
        IDistanceMatrixProvider distanceMatrix,
        IDistanceCache cache)
    {
        DistanceMatrix = distanceMatrix;
        Cache = cache;
        
        // Дефолтные настройки
        WorkingDayStart = TimeSpan.FromHours(9);
        WorkingDayEnd = TimeSpan.FromHours(18);
        DefaultServiceDurationMinutes = 15;
        TimeWindowPenalty = 50.0;
        ShortagePenaltyPerUnit = 10.0;
        CostPerKm = 15.0;
    }

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
        double costPerKm = 10.0)
    {
        PlanningDate = planningDate;
        if (workingDayStart.HasValue) 
            WorkingDayStart = workingDayStart.Value;
        if (workingDayEnd.HasValue) 
            WorkingDayEnd = workingDayEnd.Value;
        DefaultServiceDurationMinutes = defaultServiceDurationMinutes;
        TimeWindowPenalty = timeWindowPenalty;
        ShortagePenaltyPerUnit = shortagePenaltyPerUnit;
        CostPerKm = costPerKm;
        
        DepotLocation = allLocations.FirstOrDefault(l => l.Index == 1)
            ?? throw new InvalidOperationException("Depot location (Index=0) not found");
        
        _farmLocations = allLocations
            .Where(fl => fl.Id?.StartsWith("Farm_") == true)
            .ToDictionary(fl => GetFarmIdFromLocationPoint(fl), fl => fl);

        var farmIdToIndexMap = _farmLocations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Index);
        var validFarmIds = new HashSet<int>(farmIdToIndexMap.Keys);  // ← HashSet для SQL-совместимости

        var storagesList = db.FarmStorages
            .Where(fs => validFarmIds.Contains(fs.FarmId) && fs.Quantity > 0.01) 
            .ToList();  // ← синхронная загрузка

        // Формируем кэш в памяти
        _farmStocksCache = storagesList
            .ToDictionary(
                fs => (farmIdToIndexMap[fs.FarmId], fs.ProductId), 
                fs => fs.Quantity);
        
        AvailableVehicles = vehicles
            .Where(v => v.IsActive)
            .Select(v => new VehicleInfo
            {
                Id = v.Id, DepotId = v.StartPointId, Capacity = v.Capacity,
                SpeedKmph = v.SpeedKmph, CostPerKm = v.CostPerKm, IsActive = v.IsActive
            })
            .ToList();
    }

    public Dictionary<(int, int), double> GetFarmStocksCache() => _farmStocksCache;
    public void UpdateFarmStocks(Dictionary<(int, int), double> updatedStocks)
    {
        foreach (var kvp in updatedStocks)
            _farmStocksCache[kvp.Key] = kvp.Value;
    }
    public Dictionary<int, LocationPoint> GetFarmLocations() => _farmLocations;

    private int GetFarmIdFromLocationPoint(LocationPoint loc)
    {
        if (string.IsNullOrEmpty(loc.Id)) return -1;
        var parts = loc.Id.Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var id)) return id;
        return -1;
    }
}