using System.Text.Json;
using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.GraphHopper.DistanceMatrix.Cache;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.GA.Config;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess;
using DataAccess.Entity;
using DataAccess.Entity.GA;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using DataAccess.Entity.Logistics.GA.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.SubServices.Logistics.DTO;

public class DeliveryOptimizationService
{
    private readonly FarmDbContext _dbContext;
    private readonly IDistanceMatrixProvider _matrixProvider;
    private readonly ILogger<DeliveryOptimizationService>? _logger;
    private readonly ILogger<GeneticAlgorithm>? _gaLogger;
    private readonly IDistanceCache _cache;
    private readonly FitnessObjective _fitnessObjective;
    private readonly Random rand = new Random();

    private readonly GeneticAlgorithmConfig _gaConfig;
    
    public DeliveryOptimizationService(
        FarmDbContext dbContext,
        IDistanceMatrixProvider matrixProvider,
        IDistanceCache cache,
        FitnessObjective fitnessObjective = FitnessObjective.MinimizeDistance,
        ILogger<DeliveryOptimizationService>? logger = null,
        GeneticAlgorithmConfig? gaConfig = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _matrixProvider = matrixProvider ?? throw new ArgumentNullException(nameof(matrixProvider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _fitnessObjective = fitnessObjective;
        _logger = logger;
        _gaConfig = gaConfig ?? new GeneticAlgorithmConfig(); // Дефолтные параметры
    }

    // Helper для параллельного расчета улиц в хромосоме
    private async Task<(int Index, List<LocationPoint> Points)> LoadStreetSegmentAsync(
        LocationPoint from,
        LocationPoint to,
        int index,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();

        try
        {
            var points = await _matrixProvider.GetStreetGeometryAsync(from, to);
            return (index, points);
        }
        finally
        {
            semaphore.Release();
        }
    }

    // Helper для построения одного RouteDto
    private async Task<RouteDTO> BuildRouteDetailAsync(
        Chromosome route,
        int routeIndex,
        int depotId,
        IReadOnlyDictionary<int, Vehicle> vehiclesById,
        IReadOnlyDictionary<int, LocationPoint> farmsDict,
        IReadOnlyDictionary<int, LocationPoint> storesDict,
        LocationPoint depotFarm,
        IDecodingContext decodingContext,
        SemaphoreSlim geometrySemaphore)
    {
        var routeDecoder = new RouteDecoder(decodingContext, _logger);
        var routeDecoding = routeDecoder.Decode(route);

        var stops = route.Genes
            .Where(g => g.Task != null)
            .Select((g, index) =>
            {
                var isFarmLoad = g.Operation == OperationType.Load;
                var locationType = isFarmLoad ? StopType.Farm : StopType.Store;

                int? farmId = null;
                int? storeId = null;
                LocationPoint? location = null;

                if (isFarmLoad)
                {
                    farmId = g.AssignedFarmId ?? g.Task?.FarmId;

                    if (farmId.HasValue && farmsDict.TryGetValue(farmId.Value, out var farmCoord))
                    {
                        location = new LocationPoint
                        {
                            Index = LocationIdFactory.FromFarm(farmId.Value),
                            Id = farmCoord.Id,
                            Latitude = farmCoord.Latitude,
                            Longitude = farmCoord.Longitude
                        };
                    }
                }
                else
                {
                    storeId = g.Task?.StoreId;

                    if (storeId.HasValue && storesDict.TryGetValue(storeId.Value, out var storeCoord))
                    {
                        location = new LocationPoint
                        {
                            Index = LocationIdFactory.FromStore(storeId.Value),
                            Id = storeCoord.Id,
                            Latitude = storeCoord.Latitude,
                            Longitude = storeCoord.Longitude
                        };
                    }
                }

                return new RouteStopDTO
                {
                    StopIndex = index + 1,
                    LocationType = locationType,
                    FarmId = farmId,
                    StoreId = storeId,
                    Location = location,
                    ArrivalTimeUtc = null,
                    DepartureTimeUtc = null,
                    ServiceDurationMinutes = isFarmLoad ? 15 : 10,
                    Products = g.Task != null
                        ? new List<RouteStopProductDTO>
                        {
                            new RouteStopProductDTO
                            {
                                ProductId = g.Task.ProductId,
                                Quantity = g.Task.Quantity,
                                TaskId = g.TaskId
                            }
                        }
                        : new List<RouteStopProductDTO>()
                };
            })
            .ToList();

        var routeStops = new List<RouteStopDTO>(stops.Count + 2)
        {
            CreateDepotStop(depotFarm, 0)
        };

        routeStops.AddRange(stops);
        routeStops.Add(CreateDepotStop(depotFarm, stops.Count + 1));

        var streetPath = await BuildStreetPathAsync(routeStops, geometrySemaphore);

        vehiclesById.TryGetValue(route.VehicleId, out var vehicle);

        return new RouteDTO
        {
            Id = routeIndex + 1,
            VehicleId = route.VehicleId,
            DepotId = depotId,
            Stops = routeStops,
            StreetPath = streetPath,
            DistanceKm = routeDecoding.Metrics?.TotalDistance ?? 0,
            TimeHours = routeDecoding.Metrics?.TotalTimeHours ?? 0,
            TotalLoadKg = routeDecoding.Metrics?.MaxLoadKg ?? 0,
            CapacityUtilization = vehicle != null && vehicle.Capacity > 0
                ? (routeDecoding.Metrics?.MaxLoadKg ?? 0) / vehicle.Capacity * 100
                : 0
        };
    }


    // Методы для стартовой точки и построения улиц на хромосомах
    private static RouteStopDTO CreateDepotStop(LocationPoint depotLocation, int stopIndex) => new()
    {
        StopIndex = stopIndex,
        LocationType = StopType.Depot,
        Location = new LocationPoint
        {
            Index = depotLocation.Index,
            Id = depotLocation.Id,
            Latitude = depotLocation.Latitude,
            Longitude = depotLocation.Longitude
        },
        ServiceDurationMinutes = 0
    };

    private async Task<List<LocationPoint>> BuildStreetPathAsync(
        List<RouteStopDTO> stops,
        SemaphoreSlim geometrySemaphore)
    {
        var routePoints = stops
            .Where(s => s.Location != null)
            .Select(s => s.Location!)
            .ToList();

        if (routePoints.Count < 2)
            return new List<LocationPoint>();

        var segmentTasks = routePoints
            .Zip(routePoints.Skip(1), (from, to) => (from, to))
            .Select((pair, index) => LoadStreetSegmentAsync(pair.from, pair.to, index, geometrySemaphore))
            .ToList();

        var segments = await Task.WhenAll(segmentTasks);

        var result = new List<LocationPoint>();

        foreach (var segment in segments.OrderBy(s => s.Index))
        {
            if (segment.Points == null || segment.Points.Count == 0)
                continue;

            if (result.Count == 0)
                result.AddRange(segment.Points);
            else
                result.AddRange(segment.Points.Skip(1));
        }

        return result;
    }

    // Самый главный метод для оптимизации всего маршрута и соединения всех сервисов
    public async Task<RouteOptimizationResultDTO> OptimizeRouteAsync(RouteOptimizationRequestDTO request)
    {

        var monitor = new ResourceMonitor();
        monitor.Start();

        _logger?.LogInformation("Начало оптимизации на дату {Date}", request.DeliveryDate);

        // Загрузка данных
        var existingPlans = await _dbContext.DeliveryPlans
            .Include(p => p.Store)
            .Include(p => p.Items).ThenInclude(i => i.Product) // уже есть
            .Where(p => p.DeliveryDate == request.DeliveryDate
                        && request.StoreIds.Contains(p.StoreId))
            .OrderBy(p => p.StoreId)
            .AsNoTracking() // опционально, быстрее
            .ToListAsync();

        if (!existingPlans.Any())
            throw new InvalidOperationException("Нет планов доставки на указанную дату");

        var vehicles = await _dbContext.Vehicles
            .OrderBy(v => v.Id)
            .ToListAsync();
        // Преобразование планов в задачи — с защитой от null
        var tasks = existingPlans
            .Where(plan => plan.Items != null) // ← защита
            .SelectMany(plan => plan.Items.Select(item => new DeliveryTaskDTO
            {
                Id = item.Id,
                StoreId = plan.StoreId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Priority = 1.0,
                IsShortage = false,
                TimeWindowOpen = TimeSpan.FromHours(9),
                TimeWindowClose = TimeSpan.FromHours(18),
                StoreCoord = new LocationPoint
                {
                    Index = LocationIdFactory.FromStore(plan.StoreId),
                    Id = $"Store_{plan.StoreId}",
                    Latitude = plan.Store?.Latitude ?? 0, // тоже защита
                    Longitude = plan.Store?.Longitude ?? 0
                }
            }))
            .ToList();

        if (!tasks.Any())
        {
            _logger?.LogWarning("Нет задач для оптимизации после фильтрации");
            return new RouteOptimizationResultDTO
            {
                PlanningDate = request.DeliveryDate,
                Warning = "Нет задач для оптимизации (Items были null или пустые)"
            };
        }

        if (!tasks.Any())
        {
            _logger?.LogWarning("Нет задач для оптимизации после фильтрации");
            return new RouteOptimizationResultDTO
            {
                PlanningDate = request.DeliveryDate,
                Warning = "Нет задач для оптимизации"
            };
        }

        // Подготовка локаций для матрицы расстояний
        var allLocations = new List<LocationPoint>();

        // Фермы
        var farms = await _dbContext.Farms.ToListAsync();
        foreach (var farm in farms)
        {
            allLocations.Add(new LocationPoint
            {
                Index = LocationIdFactory.FromFarm(farm.Id),
                Id = $"Farm_{farm.Id}",
                Latitude = farm.Latitude,
                Longitude = farm.Longitude
            });
        }

        // Магазины (уникальные из запроса)
        var stores = await _dbContext.Stores
            .Where(s => request.StoreIds.Contains(s.Id))
            .ToListAsync();

        foreach (var store in stores)
        {
            allLocations.Add(new LocationPoint
            {
                Index = LocationIdFactory.FromStore(store.Id), // Смещение, чтобы не конфликтовать с фермами
                Id = $"Store_{store.Id}",
                Latitude = store.Latitude,
                Longitude = store.Longitude
            });
        }

        // Инициализация контекста декодирования
        var decodingContext = new DecodingContext(
            distanceMatrix: _matrixProvider,
            cache: _cache
        );

        using var cts = new CancellationTokenSource();

        var monitorTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                monitor.Tick();
                await Task.Delay(200);
            }
        });

        decodingContext.Initialize(
            db: _dbContext,
            allLocations: allLocations,
            vehicles: vehicles,
            planningDate: request.DeliveryDate);


        // Очистка кеша (если нужно)
        if (request.CacheClear == true)
            _cache.ClearCache();

        // if (request.DisableCache == true)
        // {
        //     // Режим без кеша - загружаем только нужные точки напрямую из GraphHopper
        //     _logger?.LogWarning("⚠️ CACHE DISABLED - Loading distances for current optimization directly from GraphHopper");
        //     _logger?.LogInformation($"Loading distances for {allLocations.Count} points ({allLocations.Count * (allLocations.Count - 1)} pairs)");
        //
        //     // Очищаем память от старых данных
        //     if (_cache is HybridDistanceCache hybridCache)
        //     {
        //         // Очищаем только память, не трогаем Redis
        //         // (в HybridDistanceCache нет метода для очистки только памяти, поэтому просто не загружаем из Redis)
        //     }
        //
        //     _cache.MemoryOnlyMode = false; // Разрешаем обращения к GraphHopper
        //
        //     // Предзагружаем расстояния ТОЛЬКО для текущих точек напрямую из GraphHopper
        //     await _matrixProvider.PreloadAsync(allLocations);
        //
        //     _logger?.LogInformation($"Loaded {allLocations.Count * (allLocations.Count - 1)} distances directly from GraphHopper");
        //
        //     // Переключаемся в режим памяти (данные уже загружены в память через PreloadAsync)
        //     _cache.MemoryOnlyMode = true;
        // }
        // else
        // {
        //     // Обычный режим с кешем
        //     // Прогрев кеша
        //     await _matrixProvider.EnsureCacheReady(_cache, _matrixProvider, allLocations);
        //
        //     var loadedCount = await _cache.LoadAllFromRedisToMemoryAsync(allLocations);
        //     _logger?.LogInformation($"Loaded {loadedCount} distances into memory from Redis");
        //
        //     // блокируем Redis
        //     _cache.MemoryOnlyMode = true;
        //     _logger?.LogInformation("Cache ready. Switching to MEMORY-ONLY mode");
        // }

        // Прогрев кеша
        await _matrixProvider.EnsureCacheReady(_cache, _matrixProvider, allLocations);

        var loadedCount = await _cache.LoadAllFromRedisToMemoryAsync(allLocations);
        _logger?.LogInformation($"Loaded {loadedCount} distances into memory");

        // блокируем Redis
        _cache.MemoryOnlyMode = true;

        _logger?.LogInformation("Cache ready. Switching to MEMORY-ONLY mode");

        // Создание декодера и ГА
        var decoder = new RouteDecoder(decodingContext, _logger);
        using (var ga = new GeneticAlgorithm(
                   decodingContext: decodingContext,
                   fitnessObjective: request.FitnessObjective ?? _fitnessObjective, // Или из конфига
                   populationSize: request.PopulationSize ?? _gaConfig.PopulationSize,
                   maxGenerations: request.MaxGenerations ?? _gaConfig.MaxGenerations,
                   crossoverRate: request.CrossoverRate ?? _gaConfig.CrossoverRate,
                   mutationRate: request.MutationRate ?? _gaConfig.MutationRate,
                   tournamentSize: request.TournamentSize ?? _gaConfig.TournamentSize,
                   vehicleMutationRate: request.VehicleMutationRate ?? _gaConfig.VehicleMutationRate,
                   randomSeed: request.RandomSeed,
                   farmMutationRate: request.FarmMutationRate ?? _gaConfig.FarmMutationRate,
                   logger: _gaLogger,
                   maxDegreeOfParallelism: 1
               ))
        {

            // Запуск оптимизации
            var bestSolution = await ga.OptimizeAsync(tasks);
            var optimizationResult = ga.GetOptimizationResult();


            // Сбор метрик и формирование ответа
            var resultMetrics = bestSolution.Metrics ?? new SolutionMetrics();

            // Предварительно загружаем справочники (если ещё не загружены)
            var farmsDict = await _dbContext.Farms
                .ToDictionaryAsync(
                    f => f.Id,
                    f => new LocationPoint
                    {
                        Index = LocationIdFactory.FromFarm(f.Id),
                        Id = $"Farm_{f.Id}",
                        Latitude = f.Latitude,
                        Longitude = f.Longitude
                    });

            var storesDict = await _dbContext.Stores
                .ToDictionaryAsync(
                    s => s.Id,
                    s => new LocationPoint
                    {
                        Index = LocationIdFactory.FromStore(s.Id),
                        Id = $"Store_{s.Id}",
                        Latitude = s.Latitude,
                        Longitude = s.Longitude
                    });

            if (!farmsDict.TryGetValue(request.DepotId, out var depotFarm))
                throw new InvalidOperationException($"Depot farm {request.DepotId} not found.");

            var vehiclesById = vehicles.ToDictionary(v => v.Id);

            var routesForMap = bestSolution.Routes
                .Where(r => r.Genes.Any())
                .ToList();

            using var geometrySemaphore = new SemaphoreSlim(5);

            var routeTasks = routesForMap
                .Select((route, routeIndex) => BuildRouteDetailAsync(
                    route,
                    routeIndex,
                    request.DepotId,
                    vehiclesById,
                    farmsDict,
                    storesDict,
                    depotFarm,
                    decodingContext,
                    geometrySemaphore));

            var routeDetails = (await Task.WhenAll(routeTasks))
                .OrderBy(r => r.Id)
                .ToList();


            _logger?.LogInformation(
                "Оптимизация завершена. Fitness: {Fitness}, Дистанция: {Distance}, Маршрутов: {Count}",
                optimizationResult.BestFitness, resultMetrics.TotalDistance, routeDetails.Count);

            cts.Cancel();
            await monitorTask;

            monitor.Stop();

            var run = new OptimizationRun
            {
                PlanningDate = request.DeliveryDate,
                CreationTime = DateTime.UtcNow,

                Seed = request.RandomSeed ?? -1,

                FitnessObjective = request.FitnessObjective ?? _fitnessObjective,

                PopulationSize = request.PopulationSize ?? _gaConfig.PopulationSize,
                MaxGenerations = request.MaxGenerations ?? _gaConfig.MaxGenerations,

                MutationRate = request.MutationRate ?? _gaConfig.MutationRate,
                MutationFarmRate = request.FarmMutationRate ?? _gaConfig.FarmMutationRate,
                MutationVehicleRate = request.VehicleMutationRate ?? _gaConfig.VehicleMutationRate,

                BestFitness = optimizationResult.BestFitness,
                TotalDistance = resultMetrics.TotalDistance,
                FuelCost = resultMetrics.TotalFuelCost,
                TotalVehiclesUsed = resultMetrics.TotalVehiclesUsed,
                TotalTimeViolations = resultMetrics.TotalTimeViolations,
                ExecutionTimeMs = monitor.GetElapsedMilliseconds(),
                AvgCpuUsage = monitor.GetAverageCpuUsage(),
                MaxMemoryMb = monitor.GetMaxMemoryMb()
            };

            run.FitnessHistory = optimizationResult.SolutionFitnessHistory
                .Select((fitness, index) => new FitnessHistoryPoint
                {
                    Generation = index,
                    Fitness = fitness
                })
                .ToList();

            run.Routes = routeDetails.Select(r => new OptimizationRoute
            {
                VehicleId = r.VehicleId ?? -1,
                DistanceKm = r.DistanceKm,
                TimeHours = r.TimeHours,

                GeometryJson = JsonSerializer.Serialize(r.StreetPath),
                StopsJson = JsonSerializer.Serialize(r.Stops)
            }).ToList();

            _dbContext.OptimizationRuns.Add(run);
            await _dbContext.SaveChangesAsync();

            _cache.MemoryOnlyMode = false;

            // Формируем и возвращаем результат
            return new RouteOptimizationResultDTO
            {
                Routes = routeDetails,
                Metrics = new OptimizationMetricsDTO
                {
                    TotalDistance = resultMetrics.TotalDistance,
                    FuelCost = resultMetrics.TotalFuelCost,
                    TotalVehiclesUsed = resultMetrics.TotalVehiclesUsed,
                    TotalDeadlineFail = resultMetrics.TotalTimeViolations,
                    BestFitness = optimizationResult.BestFitness
                },
                Warning = resultMetrics.TotalTimeViolations > 0
                    ? $"Нарушено временных окон: {resultMetrics.TotalTimeViolations}"
                    : null,
                PlanningDate = request.DeliveryDate,
                FitnessHistory = optimizationResult.SolutionFitnessHistory,

                RunInfo = new OptimizationRunInfoDTO
                {
                    Seed = run.Seed,
                    FitnessObjective = run.FitnessObjective,
                    PopulationSize = run.PopulationSize,
                    MaxGenerations = run.MaxGenerations,
                    CrossoverRate = request.CrossoverRate ?? _gaConfig.CrossoverRate,
                    MutationRate = request.MutationRate ?? _gaConfig.MutationRate,

                    ExecutionTimeMs = run.ExecutionTimeMs,
                    AvgCpuUsage = run.AvgCpuUsage,
                    MaxMemoryMb = run.MaxMemoryMb
                }
            };
        }
    }
}
