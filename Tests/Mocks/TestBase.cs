using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.SubServices.Logistics;
using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests;

/// <summary>
/// Базовый класс для тестов с общей настройкой моков и вспомогательными методами.
/// </summary>
public abstract class TestBase
{
    protected readonly Mock<IRouteDecoder> _mockDecoder;
    protected readonly Mock<IDecodingContext> _mockContext;
    protected readonly Mock<ILogger<GeneticAlgorithm>> _mockLogger;
    protected readonly Mock<IDistanceMatrixProvider> _mockDistanceMatrix;

    protected TestBase()
    {
        _mockDecoder = new Mock<IRouteDecoder>();
        _mockContext = new Mock<IDecodingContext>();
        _mockLogger = new Mock<ILogger<GeneticAlgorithm>>();
        _mockDistanceMatrix = new Mock<IDistanceMatrixProvider>();
        
        SetupDefaultContext();
    }

    protected virtual void SetupDefaultContext()
    {
        // 🔹 Депо всегда имеет Index = 0
        _mockContext.Setup(c => c.DepotLocation)
            .Returns(new LocationPoint(0, "Depot", 55.75, 37.61));
        
        _mockContext.Setup(c => c.WorkingDayStart).Returns(TimeSpan.FromHours(9));
        _mockContext.Setup(c => c.WorkingDayEnd).Returns(TimeSpan.FromHours(18));
        _mockContext.Setup(c => c.PlanningDate).Returns(DateOnly.FromDateTime(DateTime.Today));
        
        _mockContext.Setup(c => c.AvailableVehicles)
            .Returns(new List<VehicleInfo>
            {
                new() { Id = 1, Capacity = 1000, CostPerKm = 15, SpeedKmph = 40, IsActive = true },
                new() { Id = 2, Capacity = 1500, CostPerKm = 18, SpeedKmph = 45, IsActive = true }
            });

        // Базовый fallback для матрицы расстояний: 10 км для любых точек
        _mockDistanceMatrix.Setup(m => m.GetDistanceWithCache(It.IsAny<LocationPoint>(), It.IsAny<LocationPoint>()))
            .Returns(10.0);
        
        _mockContext.Setup(c => c.DistanceMatrix).Returns(_mockDistanceMatrix.Object);
        
        // Дефолтная ферма с Index = 1
        SetupFarmLocations(new Dictionary<int, LocationPoint>
        {
            { 1, new LocationPoint(1, "Farm_1", 55.8, 37.7) }
        });
    }

    /// <summary>
    /// Настраивает моки для GetFarmLocations() с переданным словарём ферм.
    /// </summary>
    protected void SetupFarmLocations(Dictionary<int, LocationPoint> farmLocations) =>
        _mockContext.Setup(c => c.GetFarmLocations()).Returns(farmLocations);

    /// <summary>
    /// 🔥 НАДЁЖНАЯ настройка дистанций по INDEX точек.
    /// Использует callback для динамического возврата расстояний.
    /// </summary>
    /// <param name="routes">Массив кортежей (fromIndex, toIndex, distance)</param>
    protected void SetupDistanceMatrixByIndex(params (int from, int to, double distance)[] routes)
    {
        var lookup = routes.ToDictionary(
            r => (r.from, r.to), 
            r => r.distance,
            EqualityComparer<(int, int)>.Default
        );

        _mockDistanceMatrix
            .Setup(m => m.GetDistanceWithCache(It.IsAny<LocationPoint>(), It.IsAny<LocationPoint>()))
            .Returns((LocationPoint from, LocationPoint to) =>
            {
                var key = (from.Index, to.Index);
                if (lookup.TryGetValue(key, out var dist))
                    return dist;

                Console.WriteLine($"[MISSING DISTANCE] {from.Index} → {to.Index} ({from.Id} → {to.Id})");
                return 10.0;
            });
    }

    /// <summary>
    /// Создаёт список тестовых задач с автоматически назначенными индексами для StoreCoord.
    /// </summary>
    protected List<DeliveryTaskDTO> CreateTestTasks(int count, int? fixedFarmId = null)
    {
        var tasks = new List<DeliveryTaskDTO>();
        for (int i = 1; i <= count; i++)
        {
            tasks.Add(new DeliveryTaskDTO
            {
                Id = i,
                FarmId = fixedFarmId ?? i,
                StoreId = i + 100,
                ProductId = i * 10,
                Quantity = 50 + i * 10,
                Priority = 1.0,
                IsShortage = false,
                TimeWindowOpen = TimeSpan.FromHours(9),
                TimeWindowClose = TimeSpan.FromHours(18),
                // 🔹 Index = 100+i для уникальности, чтобы не пересекаться с фермами (1..99)
                StoreCoord = new LocationPoint(100 + i, $"Store_{100 + i}", 55.75 + i * 0.01, 37.61 + i * 0.01)
            });
        }
        return tasks;
    }
    
    /// <summary>
    /// Создаёт одну тестовую задачу с параметрами.
    /// </summary>
    protected DeliveryTaskDTO CreateTask(int id, int? farmId = null, double quantity = 10, bool isShortage = false) => new()
    {
        Id = id,
        FarmId = farmId,
        StoreId = 1,
        ProductId = 1,
        Quantity = quantity,
        IsShortage = isShortage,
        // 🔹 Index = 100+id для магазина
        StoreCoord = new LocationPoint(100 + id, $"Store_{100 + id}", 55.0 + id * 0.01, 37.0 + id * 0.01),
        Priority = 1.0,
        TimeWindowOpen = TimeSpan.FromHours(9),
        TimeWindowClose = TimeSpan.FromHours(18)
    };

    /// <summary>
    /// Создаёт валидную хромосому с операциями Load перед Unload для каждой задачи.
    /// </summary>
    protected Chromosome CreateValidChromosome(int vehicleId, params (int taskId, OperationType op)[] operations)
    {
        var chromosome = new Chromosome(vehicleId);
        var maxTaskId = operations.Max(x => x.taskId);
        var tasks = CreateTestTasks(maxTaskId);
        
        // Сортируем: сначала все Load, потом все Unload (для валидности)
        foreach (var (taskId, op) in operations.OrderBy(o => o.op == OperationType.Load ? 0 : 1))
        {
            var task = tasks.First(t => t.Id == taskId);
            chromosome.Genes.Add(new Gene(taskId, op, task));
        }
        
        return chromosome;
    }

    /// <summary>
    /// Создаёт хромосому с произвольным набором генов (для тестов с нарушением порядка).
    /// </summary>
    protected Chromosome CreateChromosomeWithGenes(int vehicleId, params Gene[] genes)
    {
        var chromosome = new Chromosome(vehicleId);
        chromosome.Genes.AddRange(genes);
        return chromosome;
    }
}