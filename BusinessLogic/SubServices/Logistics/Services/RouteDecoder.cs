using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity;
using DataAccess.Entity.GA;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.SubServices.Logistics.DTO;

public class RouteDecoder : IRouteDecoder
{
    private readonly IDecodingContext _context;
    private readonly ILogger? _logger;

    // Параметры симуляции (можно вынести в конфиг)
    private const double AverageSpeedKmh = 40.0;        // Средняя скорость, км/ч
    private const double ServiceTimeMinutes = 5.0;      // Время на погрузку/выгрузку, мин

    public RouteDecoder(IDecodingContext context, ILogger? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public DecodingResult Decode(Chromosome chromosome)
    {
        
        var metrics = new ChromosomeMetrics();

        if (chromosome?.Genes == null || chromosome.Genes.Count == 0)
        {
            return new DecodingResult 
            { 
                IsValid = true, 
                Metrics = metrics 
            };
        }
        
        // Фильтруем гены: исключаем задачи с дефицитом
        var genes = chromosome.Genes.ToList();

        if (!genes.Any())
        {
            return new DecodingResult { Metrics = metrics };
        }

        // Получаем данные о машине
        var vehicle = _context.AvailableVehicles
            .FirstOrDefault(v => v.Id == chromosome.VehicleId && v.IsActive);
        
        if (vehicle == null)
        {
            _logger?.LogWarning("Vehicle {VehicleId} not found or inactive", chromosome.VehicleId);
            return new DecodingResult { Metrics = metrics };
        }

        var depot = _context.DepotLocation;
        if (depot == null)
        {
            _logger?.LogError("Depot location not configured");
            return new DecodingResult { Metrics = metrics };
        }

        // Инициализация состояния симуляции
        var currentLocation = depot;
        var currentLoad = 0.0;                      // Текущий вес груза в кузове
        var totalDistance = 0.0;
        var timeWindowViolations = 0;
        var maxLoad = 0.0;
        var productViolations = 0;
        var moreMaxKg = 0;
        var currentTime = _context.PlanningDate.ToDateTime(TimeOnly.FromTimeSpan(_context.WorkingDayStart));
        var workingDayEnd = _context.PlanningDate.ToDateTime(TimeOnly.FromTimeSpan(_context.WorkingDayEnd));

        // Отслеживаем наличие товаров в кузове (ProductId → количество)
        var cargo = new Dictionary<int, double>();

        // Симуляция выполнения маршрута: шаг за шагом
        foreach (var gene in genes)
        {
            LocationPoint? nextLocation = null;

            if (gene.Operation == OperationType.Load)
            {
                int farmIdToUse = gene.AssignedFarmId ?? gene.Task.FarmId ?? 0;
                var farm = _context.GetFarmLocations().FirstOrDefault(f => f.Key == farmIdToUse);
                if (farm.Value == null)
                {
                    _logger?.LogWarning("Farm {FarmId} location not found", farmIdToUse);
                    continue;
                }

                nextLocation = farm.Value;
            }
            else
            {
                nextLocation = gene.Task.StoreCoord;
            }

            if (nextLocation == null || currentLocation == null)
                continue;

            var distance = _context.DistanceMatrix.GetDistanceWithCache(currentLocation, nextLocation);
            var speed = vehicle.SpeedKmph > 0 ? vehicle.SpeedKmph : AverageSpeedKmh;
            var travelTimeMinutes = distance / speed * 60.0;

            totalDistance += distance;

            var arrivalTime = currentTime.AddMinutes(travelTimeMinutes);
            currentLocation = nextLocation;

            if (gene.Operation == OperationType.Unload &&
                gene.Task.TimeWindowOpen.HasValue &&
                gene.Task.TimeWindowClose.HasValue)
            {
                var windowOpen = arrivalTime.Date.Add(gene.Task.TimeWindowOpen.Value);
                var windowClose = arrivalTime.Date.Add(gene.Task.TimeWindowClose.Value);

                if (arrivalTime < windowOpen)
                {
                    arrivalTime = windowOpen; // ждём у магазина
                }
                else if (arrivalTime > windowClose)
                {
                    timeWindowViolations++;
                }
            }

            if (gene.Operation == OperationType.Load)
            {
                currentLoad += gene.Task.Quantity;
                cargo[gene.Task.ProductId] = cargo.GetValueOrDefault(gene.Task.ProductId, 0) + gene.Task.Quantity;

                maxLoad = Math.Max(maxLoad, currentLoad);
                if (currentLoad > vehicle.Capacity)
                    moreMaxKg++;
            }
            else
            {
                var available = cargo.GetValueOrDefault(gene.Task.ProductId, 0);
                if (available >= gene.Task.Quantity - 0.01)
                {
                    currentLoad -= gene.Task.Quantity;
                    cargo[gene.Task.ProductId] = available - gene.Task.Quantity;
                }
                else
                {
                    _logger?.LogWarning("Unload without load for Task {TaskId}", gene.TaskId);
                    productViolations++;
                }
            }

            currentTime = arrivalTime.AddMinutes(ServiceTimeMinutes);
        }
        

        // Возврат на депо в конце маршрута
        if (currentLocation != null && !LocationsEqual(currentLocation, depot))
        {
            var returnDistance = _context.DistanceMatrix.GetDistanceWithCache(currentLocation, depot);
            var returnTravelTime = returnDistance / AverageSpeedKmh * 60;
            
            totalDistance += returnDistance;
            currentTime = currentTime.AddMinutes(returnTravelTime);
        }

        // Заполнение метрик
        var workingDayDuration = (_context.WorkingDayEnd - _context.WorkingDayStart).TotalHours;
        
        metrics.TotalDistance = Math.Round(totalDistance, 2);
        metrics.TotalTimeHours = Math.Round((currentTime - _context.PlanningDate.ToDateTime(TimeOnly.FromTimeSpan(_context.WorkingDayStart))).TotalHours, 2);
        metrics.RouteStartTime = _context.WorkingDayStart;
        metrics.RouteEndTime = currentTime.TimeOfDay;
        metrics.FuelCost = Math.Round(totalDistance * vehicle.CostPerKm, 2);
        metrics.MaxLoadKg = Math.Round(maxLoad, 2);
        metrics.FinalLoadKg = Math.Round(currentLoad, 2); // Остаток груза = штраф
        metrics.CapacityUtilizationPercent = vehicle.Capacity > 0 
            ? Math.Round(maxLoad / vehicle.Capacity * 100, 1) 
            : 0;
        metrics.TimeWindowViolations = timeWindowViolations;
        metrics.ProductViolations = productViolations;
        metrics.LoadMoreMaxKg = moreMaxKg;

        // Флаги валидности
        var isValid = currentLoad <= 0.01 
                      && timeWindowViolations == 0 
                      && moreMaxKg == 0                     // ← добавь
                      && currentTime <= workingDayEnd.AddMinutes(30);

        metrics.LoadMoreMaxKg = moreMaxKg;

        if (!isValid)
        {
            _logger?.LogDebug("Route {VehicleId} validation: Load={FinalLoad}, Violations={Viol}, EndTime={EndTime}",
                chromosome.VehicleId, currentLoad, timeWindowViolations, currentTime);
        }

        return new DecodingResult 
        { 
            Metrics = metrics,
            IsValid = isValid
        };
    }

    // Вспомогательный метод сравнения координат
    private bool LocationsEqual(LocationPoint a, LocationPoint b)
    {
        return Math.Abs(a.Latitude - b.Latitude) < 1e-6 
            && Math.Abs(a.Longitude - b.Longitude) < 1e-6;
    }
}