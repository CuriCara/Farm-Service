using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Xunit;

namespace Tests.UnitTests;

/// <summary>
/// Тесты для RouteDecoder с полной изоляцией зависимостей.
/// Все расстояния настраиваются по индексам точек для надёжности.
/// </summary>
public class RouteDecoderTests : TestBase
{
    #region Constants for Point Indices
    private const int DepotIdx = 0;
    private const int Farm1Idx = 1;
    private const int Farm2Idx = 2;
    private const int Store1Idx = 101;
    private const int Store2Idx = 102;
    #endregion

    #region Distance Calculation Tests

    [Fact]
    public void Decode_SingleTask_CalculatesDistanceCorrectly()
    {
        var task = CreateTask(1, farmId: 1);
        task.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);

        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7) } });

        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 15.0),
            (Store1Idx, DepotIdx, 12.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task),
            new Gene(1, OperationType.Unload, task)
        );

        var result = new RouteDecoder(_mockContext.Object, _mockLogger.Object).Decode(chromosome);

        Assert.True(result.IsValid);
        Assert.Equal(37.0, result.Metrics.TotalDistance, 1);   // 10 + 15 + 12
    }

    [Fact]
    public void Decode_MultipleTasks_SumsDistancesCorrectly()
    {
        // Arrange
        var tasks = CreateTestTasks(2, fixedFarmId: 1);
        tasks[0].StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", tasks[0].StoreCoord.Latitude, tasks[0].StoreCoord.Longitude);
        tasks[1].StoreCoord = new LocationPoint(Store2Idx, $"Store_{Store2Idx}", tasks[1].StoreCoord.Latitude, tasks[1].StoreCoord.Longitude);
    
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7) } });

        // Реальный маршрут, который строит декодер:
        // Depot → Farm1 (Load1) 
        // Farm1 → Farm1 (Load2 — уже на месте)
        // Farm1 → Store1 (Unload1)
        // Store1 → Store2 (Unload2)
        // Store2 → Depot (возврат)
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Farm1Idx, 0.0),     // Load2 — уже на ферме
            (Farm1Idx, Store1Idx, 5.0),
            (Store1Idx, Store2Idx, 8.0),   // ← критический переход!
            (Store2Idx, DepotIdx, 7.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, tasks[0]),
            new Gene(2, OperationType.Load, tasks[1]),
            new Gene(1, OperationType.Unload, tasks[0]),
            new Gene(2, OperationType.Unload, tasks[1])
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.True(result.IsValid, "Route should be valid");
        Assert.Equal(30.0, result.Metrics.TotalDistance, 1);   // 10 + 0 + 5 + 8 + 7 = 30
    }

    #endregion

    #region Farm Assignment Tests

    [Fact]
    public void Decode_UnloadWithAssignedFarmId_UsesAssignedFarm()
    {
        var task = CreateTask(1, farmId: 1);
        task.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);

        var farm1 = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        var farm2 = new LocationPoint(Farm2Idx, "Farm_2", 56.0, 38.0);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farm1 }, { 2, farm2 } });

        // Важно: assignedFarmId должен быть на Load-гене, а не на Unload!
        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task, assignedFarmId: 2),   // ← Load на Farm2
            new Gene(1, OperationType.Unload, task, assignedFarmId: 2)
        );

        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm2Idx, 20.0),
            (Farm2Idx, Store1Idx, 25.0),
            (Store1Idx, DepotIdx, 15.0)
        );

        var result = new RouteDecoder(_mockContext.Object, _mockLogger.Object).Decode(chromosome);

        Assert.True(result.IsValid);
        Assert.Equal(60.0, result.Metrics.TotalDistance, 1);   // 20 + 25 + 15
    }

    [Fact]
    public void Decode_UnloadWithoutAssignedFarmId_UsesTaskFarmId()
    {
        // Arrange
        var task = CreateTask(1, farmId: 1);
        task.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });

        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 15.0),
            (Store1Idx, DepotIdx, 12.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task, assignedFarmId: null),
            new Gene(1, OperationType.Unload, task, assignedFarmId: null)
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert: 10 + 15 + 12 = 37
        Assert.True(result.IsValid);
        Assert.Equal(37.0, result.Metrics.TotalDistance, 1);
    }

    [Fact]
    public void Decode_UnloadWithNullFarmId_HandlesGracefully()
    {
        // Arrange
        var task = CreateTask(1, farmId: null);
        
        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task, null),
            new Gene(1, OperationType.Unload, task, null)
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act & Assert
        var result = decoder.Decode(chromosome);
        
        Assert.NotNull(result);
        Assert.NotNull(result.Metrics);
        Assert.True(!result.IsValid || result.Metrics.TotalDistance >= 0);
    }

    [Fact]
    public void Decode_MultipleUnloads_DifferentAssignedFarms()
    {
        var task1 = CreateTask(1, farmId: 1);
        var task2 = CreateTask(2, farmId: 1);
        task1.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", task1.StoreCoord.Latitude, task1.StoreCoord.Longitude);
        task2.StoreCoord = new LocationPoint(Store2Idx, $"Store_{Store2Idx}", task2.StoreCoord.Latitude, task2.StoreCoord.Longitude);

        var farm1 = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        var farm2 = new LocationPoint(Farm2Idx, "Farm_2", 56.0, 38.0);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farm1 }, { 2, farm2 } });

        // Реальный маршрут:
        // Depot → Farm1 (Load1) → Store1 (Unload1) → Farm2 (Load2) → Store2 (Unload2) → Depot
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 5.0),
            (Store1Idx, Farm2Idx, 8.0),   // после Unload1 едем на Farm2 для Load2
            (Farm2Idx, Store2Idx, 6.0),
            (Store2Idx, DepotIdx, 11.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task1, null),
            new Gene(1, OperationType.Unload, task1, assignedFarmId: 1),
            new Gene(2, OperationType.Load, task2, assignedFarmId: 2),   // ← Load на Farm2
            new Gene(2, OperationType.Unload, task2, assignedFarmId: 2)
        );

        var result = new RouteDecoder(_mockContext.Object, _mockLogger.Object).Decode(chromosome);

        Assert.True(result.IsValid);
        Assert.Equal(40.0, result.Metrics.TotalDistance, 1);   // 10 + 5 + 8 + 6 + 11 = 40
    }

    #endregion

    #region Capacity & Load Tests

    [Fact]
    public void Decode_CalculatesMaxLoadKg_Correctly()
    {
        // Arrange
        var tasks = CreateTestTasks(2, fixedFarmId: 1);
        tasks[0].Quantity = 300;
        tasks[1].Quantity = 400;
        tasks[0].StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", tasks[0].StoreCoord.Latitude, tasks[0].StoreCoord.Longitude);
        tasks[1].StoreCoord = new LocationPoint(Store2Idx, $"Store_{Store2Idx}", tasks[1].StoreCoord.Latitude, tasks[1].StoreCoord.Longitude);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 5.0),
            (Store1Idx, Store2Idx, 8.0),
            (Store2Idx, DepotIdx, 7.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, tasks[0]),
            new Gene(2, OperationType.Load, tasks[1]),
            new Gene(1, OperationType.Unload, tasks[0]),
            new Gene(2, OperationType.Unload, tasks[1])
        ); 

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.Equal(700, result.Metrics.MaxLoadKg);
    }

    [Fact]
    public void Decode_DetectsCapacityOverload()
    {
        // Arrange
        var tasks = CreateTestTasks(2, fixedFarmId: 1);
        tasks[0].Quantity = 800;
        tasks[1].Quantity = 800;
        tasks[0].StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", tasks[0].StoreCoord.Latitude, tasks[0].StoreCoord.Longitude);
        tasks[1].StoreCoord = new LocationPoint(Store2Idx, $"Store_{Store2Idx}", tasks[1].StoreCoord.Latitude, tasks[1].StoreCoord.Longitude);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 5.0),
            (Store1Idx, Store2Idx, 8.0),
            (Store2Idx, DepotIdx, 7.0)
        );

        _mockContext.Setup(c => c.AvailableVehicles)
            .Returns(new List<VehicleInfo>
            {
                new() { Id = 1, Capacity = 1000, CostPerKm = 15, IsActive = true }
            });

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, tasks[0]),
            new Gene(2, OperationType.Load, tasks[1]),
            new Gene(1, OperationType.Unload, tasks[0]),
            new Gene(2, OperationType.Unload, tasks[1])
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.Equal(1600, result.Metrics.MaxLoadKg);
        Assert.True(result.Metrics.MaxLoadKg > 1000);
        Assert.True(!result.IsValid || result.Metrics.ProductViolations > 0);
    }

    [Fact]
    public void Decode_AlternatingLoadUnload_AvoidsOverload()
    {
        // Arrange
        var tasks = CreateTestTasks(2, fixedFarmId: 1);
        tasks[0].Quantity = 800;
        tasks[1].Quantity = 800;
        tasks[0].StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", tasks[0].StoreCoord.Latitude, tasks[0].StoreCoord.Longitude);
        tasks[1].StoreCoord = new LocationPoint(Store2Idx, $"Store_{Store2Idx}", tasks[1].StoreCoord.Latitude, tasks[1].StoreCoord.Longitude);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 5.0),
            (Store1Idx, Farm1Idx, 5.0),
            (Farm1Idx, Store2Idx, 6.0),
            (Store2Idx, DepotIdx, 7.0)
        );

        _mockContext.Setup(c => c.AvailableVehicles)
            .Returns(new List<VehicleInfo>
            {
                new() { Id = 1, Capacity = 1000, CostPerKm = 15, IsActive = true }
            });

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, tasks[0]),
            new Gene(1, OperationType.Unload, tasks[0]),
            new Gene(2, OperationType.Load, tasks[1]),
            new Gene(2, OperationType.Unload, tasks[1])
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.Equal(800, result.Metrics.MaxLoadKg);
        Assert.True(result.Metrics.MaxLoadKg <= 1000);
    }

    #endregion

    #region Time Window Tests

    [Fact]
    public void Decode_WithinTimeWindow_NoViolations()
    {
        // Arrange
        var task = CreateTask(1, farmId: 1);
        task.TimeWindowOpen = TimeSpan.FromHours(9);
        task.TimeWindowClose = TimeSpan.FromHours(18);
        task.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 5.0),
            (Store1Idx, DepotIdx, 12.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task),
            new Gene(1, OperationType.Unload, task)
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.Metrics.TimeWindowViolations);
    }

    [Fact]
    public void Decode_OutsideTimeWindow_DetectsViolation()
    {
        // Arrange
        var task = CreateTask(1, farmId: 1);
        task.TimeWindowOpen = TimeSpan.FromHours(10);
        task.TimeWindowClose = TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(5));
        task.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 100.0),
            (Farm1Idx, Store1Idx, 100.0),
            (Store1Idx, DepotIdx, 100.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task),
            new Gene(1, OperationType.Unload, task)
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.True(result.Metrics.TimeWindowViolations > 0 || !result.IsValid,
            "Должно быть зафиксировано нарушение временного окна");
    }

    #endregion

    #region Shortage & Edge Cases

    [Fact]
    public void Decode_ExcludesShortageTasks()
    {
        // Arrange
        var task1 = CreateTask(1, farmId: 1, isShortage: false);
        var task2 = CreateTask(2, farmId: 2, isShortage: true);
        task1.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);
        task2.StoreCoord = new LocationPoint(Store2Idx, $"Store_{Store2Idx}", 55.90, 37.80);
        
        var farm1 = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farm1 } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 5.0),
            (Store1Idx, DepotIdx, 12.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task1),
            new Gene(1, OperationType.Unload, task1),
            new Gene(2, OperationType.Load, task2),
            new Gene(2, OperationType.Unload, task2)
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert: только задача 1: 10 + 5 + 12 = 27
        Assert.True(result.IsValid);
        Assert.Equal(27.0, result.Metrics.TotalDistance, 1);
        Assert.Equal(task1.Quantity, result.Metrics.MaxLoadKg);
    }

    [Fact]
    public void Decode_EmptyChromosome_ReturnsValidEmptyResult()
    {
        // Arrange
        var chromosome = new Chromosome(1);
        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Metrics);
        Assert.Equal(0, result.Metrics.TotalDistance);
        Assert.Equal(0, result.Metrics.MaxLoadKg);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Decode_LoadAfterUnload_StillCalculatesDistance()
    {
        // Arrange
        var task = CreateTask(1, farmId: 1);
        task.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 15.0),
            (Store1Idx, DepotIdx, 12.0)
        );

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Unload, task),
            new Gene(1, OperationType.Load, task) 
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert
        Assert.True(result.Metrics.TotalDistance > 0);
        Assert.True(result.Metrics.ProductViolations > 0 || !result.IsValid);
    }

    #endregion

    #region Fuel Cost Tests

    [Fact]
    public void Decode_CalculatesFuelCost_BasedOnDistance()
    {
        // Arrange
        var task = CreateTask(1, farmId: 1);
        // ✅ ИСПРАВЛЕНО: правильный конструктор
        task.StoreCoord = new LocationPoint(Store1Idx, $"Store_{Store1Idx}", 55.85, 37.75);
        
        var farmLoc = new LocationPoint(Farm1Idx, "Farm_1", 55.8, 37.7);
        SetupFarmLocations(new Dictionary<int, LocationPoint> { { 1, farmLoc } });
        
        SetupDistanceMatrixByIndex(
            (DepotIdx, Farm1Idx, 10.0),
            (Farm1Idx, Store1Idx, 15.0),
            (Store1Idx, DepotIdx, 12.0)
        );

        _mockContext.Setup(c => c.AvailableVehicles)
            .Returns(new List<VehicleInfo>
            {
                new() { Id = 1, Capacity = 1000, CostPerKm = 15, SpeedKmph = 40, IsActive = true }
            });

        var chromosome = CreateChromosomeWithGenes(1,
            new Gene(1, OperationType.Load, task),
            new Gene(1, OperationType.Unload, task)
        );

        var decoder = new RouteDecoder(_mockContext.Object, _mockLogger.Object);

        // Act
        var result = decoder.Decode(chromosome);

        // Assert: 37 км * 15 = 555
        Assert.Equal(37.0, result.Metrics.TotalDistance, 1);
        Assert.Equal(555.0, result.Metrics.FuelCost, 1);
    }

    #endregion
}