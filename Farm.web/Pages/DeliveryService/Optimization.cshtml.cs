using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.GraphHopper.DistanceMatrix.Cache;
using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using DataAccess;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace Farm.web.Pages.DeliveryService;

[Authorize(Roles = "Admin")]
public class OptimizationModel : PageModel
{
    private readonly DeliveryOptimizationService _deliveryOptimization;
    private readonly FarmDbContext _dbContext;

    public RouteOptimizationResultDTO? Result { get; private set; }
    public List<string> Logs { get; } = new();
    public bool IsRunning { get; private set; }
    public TimeSpan? ExecutionTime { get; private set; }
    public IReadOnlyList<FitnessObjective> FitnessObjectives { get; } = Enum.GetValues<FitnessObjective>();
    public int AvailableStoreCount { get; private set; }
    public int StoreCountMax => Math.Max(1, AvailableStoreCount);
    
    private readonly DistanceMatrixPreloadService _preloadService;
    private readonly IDistanceCache _cache;
    private readonly RedisDistanceCache _redis;

    public bool IsCacheLoaded { get; private set; }
    public int CachedPointsCount { get; private set; }


    [BindProperty]
    public OptimizationRunInput Input { get; set; } = new();

    public OptimizationModel(
        DeliveryOptimizationService deliveryOptimization,
        FarmDbContext dbContext,
        IDistanceCache cache, 
        DistanceMatrixPreloadService preloadService,
        RedisDistanceCache redis)
    {
        _deliveryOptimization = deliveryOptimization;
        _dbContext = dbContext;
        _cache = cache;
        _preloadService = preloadService;
        _redis = redis;
    }

    public async Task<IActionResult> OnPostDropCacheAsync()
    {
        await EnsurePlansExistAsync(Input.TestDate);
        await LoadAvailableStoreCountAsync(Input.TestDate);
        
        _redis.ClearCache();
        
        return Page();
    }
    
    public async Task<IActionResult> OnPostLoadCacheAsync()
    {
        try
        {
            Logs.Add("🔄 Загрузка матрицы расстояний из Redis в память...");
        
            var farms = await _dbContext.Farms.ToListAsync();
            var stores = await _dbContext.Stores.ToListAsync();
        
            var allPoints = new List<LocationPoint>();
        
            // Добавляем фермы
            foreach (var farm in farms)
            {
                allPoints.Add(new LocationPoint
                {
                    Index = LocationIdFactory.FromFarm(farm.Id),
                    Id = $"Farm_{farm.Id}",
                    Latitude = farm.Latitude,
                    Longitude = farm.Longitude
                });
            }
        
            // Добавляем магазины
            foreach (var store in stores)
            {
                allPoints.Add(new LocationPoint
                {
                    Index = LocationIdFactory.FromStore(store.Id),
                    Id = $"Store_{store.Id}",
                    Latitude = store.Latitude,
                    Longitude = store.Longitude
                });
            }
        
            Logs.Add($"📊 Всего точек: {allPoints.Count} (фермы: {farms.Count}, магазины: {stores.Count})");
        
            var loadedCount = await _cache.LoadAllFromRedisToMemoryAsync(allPoints);
        
            Logs.Add($"✅ Загружено {loadedCount} расстояний в память");
            IsCacheLoaded = true;
            CachedPointsCount = loadedCount;
        }
        catch (Exception ex)
        {
            Logs.Add($"❌ Ошибка загрузки кеша: {ex.Message}");
            IsCacheLoaded = false;
        }
    
        await EnsurePlansExistAsync(Input.TestDate);
        await LoadAvailableStoreCountAsync(Input.TestDate);
        
        return Page();
    }


    public async Task<IActionResult> OnGetAsync()
    {
        await EnsurePlansExistAsync(Input.TestDate);
        await LoadAvailableStoreCountAsync(Input.TestDate);
        
        // var stats = await _preloadService.GetCacheStatisticsAsync();
        // IsCacheLoaded = stats.CachePercentage > 95; // Считаем загруженным если >95%
        // CachedPointsCount = stats.CachedPairs;

        
        return Page();
    }

    public async Task<IActionResult> OnPostRunAsync()
    {
        IsRunning = true;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logs.Clear();

            await EnsurePlansExistAsync(Input.TestDate);
            await LoadAvailableStoreCountAsync(Input.TestDate);

            var farms = await _dbContext.Farms.ToListAsync();
            var stores = await _dbContext.Stores.ToListAsync();
        
            var allPoints = new List<LocationPoint>();
        
            foreach (var farm in farms)
            {
                allPoints.Add(new LocationPoint
                {
                    Index = LocationIdFactory.FromFarm(farm.Id),
                    Id = $"Farm_{farm.Id}",
                    Latitude = farm.Latitude,
                    Longitude = farm.Longitude
                });
            }
        
            foreach (var store in stores)
            {
                allPoints.Add(new LocationPoint
                {
                    Index = LocationIdFactory.FromStore(store.Id),
                    Id = $"Store_{store.Id}",
                    Latitude = store.Latitude,
                    Longitude = store.Longitude
                });
            }
        
            // Проверяем сколько записей в памяти
            int memoryCount = 0;
            if (_cache is HybridDistanceCache hybridCache)
            {
                memoryCount = hybridCache.GetMemoryCacheCount(allPoints);
            }
        
            int totalPairs = allPoints.Count * (allPoints.Count - 1);
            double cachePercentage = totalPairs > 0 ? memoryCount * 100.0 / totalPairs : 0;
        
            Logs.Add($"📊 Кеш в памяти: {memoryCount}/{totalPairs} ({cachePercentage:F2}%)");
        
            // // Если кеш заполнен менее чем на 95%, загружаем
            // if (cachePercentage < 95.0)
            // {
            //     Logs.Add("⚠️ Кеш не загружен. Загружаем из Redis...");
            //     var loadedCount = await _cache.LoadAllFromRedisToMemoryAsync(allPoints);
            //     Logs.Add($"✅ Загружено {loadedCount} расстояний в память");
            // }
            // else
            // {
            //     IsCacheLoaded = true;
            //     Logs.Add("✅ Кеш уже загружен в память");
            // }
            
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (AvailableStoreCount == 0)
            {
                throw new InvalidOperationException("На выбранную дату не найдено ни одного магазина для построения теста.");
            }

            var selectedStoreCount = Math.Clamp(Input.StoreCount, 1, StoreCountMax);

            var allStoreIds = await _dbContext.DeliveryPlans
                .Where(p => p.DeliveryDate == Input.TestDate)
                .Select(p => p.StoreId)
                .Distinct()
                .OrderBy(id => id)
                .ToListAsync();
            
            var selectionRandom = new Random(Input.Seed);

            // Перемешиваем детерминированно
            var storeIds = allStoreIds
                .OrderBy(_ => selectionRandom.Next())
                .Take(selectedStoreCount)
                .ToList();

            var depot = await _dbContext.Farms
                .OrderBy(f => f.Id)
                .FirstAsync();

            var request = new RouteOptimizationRequestDTO
            {
                DeliveryDate = Input.TestDate,
                StoreIds = storeIds,
                DepotId = depot.Id,
                FitnessObjective = Input.FitnessObjective,
                PopulationSize = Input.PopulationSize,
                MaxGenerations = Input.MaxGenerations,
                CrossoverRate = Input.CrossoverRate,
                MutationRate = Input.MutationRate,
                TournamentSize = Input.TournamentSize,
                VehicleMutationRate = Input.VehicleMutationRate,
                FarmMutationRate = Input.FarmMutationRate,
                CacheClear = false,
                RandomSeed = Input.Seed,
                DisableCache = Input.DisableCache
            };

            Logs.Add($"🚀 Запуск оптимизации: {storeIds.Count} магазинов, {Input.TestDate}");
            Logs.Add(
                $"⚙ GA: популяция {Input.PopulationSize}, поколений {Input.MaxGenerations}, " +
                $"fitness {Input.FitnessObjective}, crossover {Input.CrossoverRate:F2}, mutation {Input.MutationRate:F2}");
            Logs.Add(
                $"🔀 Мутации: vehicle {Input.VehicleMutationRate:F2}, farm {Input.FarmMutationRate:F2}, " +
                $"tournament {Input.TournamentSize}");

            Result = await _deliveryOptimization.OptimizeRouteAsync(request);

            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed;

            Logs.Add($"✅ Оптимизация завершена за {ExecutionTime.Value.TotalSeconds:F1} сек");
            Logs.Add($"📊 Лучший fitness: {Result.Metrics.BestFitness:F4}");
            Logs.Add($"🛣 Дистанция: {Result.Metrics.TotalDistance:F1} км");
            Logs.Add($"⛽ Стоимость топлива: {Result.Metrics.FuelCost:F0} ₽");
            Logs.Add($"🚚 Машин использовано: {Result.Metrics.TotalVehiclesUsed}");
            Logs.Add($"⚠ Нарушений окон: {Result.Metrics.TotalDeadlineFail}");

            if (Result.FitnessHistory?.Any() == true)
            {
                Logs.Add(
                    $"📈 История fitness (последние 5): " +
                    string.Join(" → ", Result.FitnessHistory.TakeLast(5).Select(f => f.ToString("F4"))));
            }
        }
        catch (Exception ex)
        {
            Logs.Add($"❌ ОШИБКА: {ex.Message}");
            Logs.Add($"Stack: {ex.StackTrace}");
        }
        finally
        {
            IsRunning = false;
        }

        return Page();
    }

    private async Task EnsurePlansExistAsync(DateOnly date)
    {
        var hasPlans = await _dbContext.DeliveryPlans
            .AnyAsync(p => p.DeliveryDate == date);

        if (hasPlans)
        {
            return;
        }

        await FarmDbInitializer.CreateDeliveryPlanForTestAsync(_dbContext, date);
        Logs.Add($"✅ Созданы тестовые планы доставки на {date}");
    }

    private async Task LoadAvailableStoreCountAsync(DateOnly date)
    {
        AvailableStoreCount = await _dbContext.DeliveryPlans
            .Where(p => p.DeliveryDate == date)
            .Select(p => p.StoreId)
            .Distinct()
            .CountAsync();

        if (AvailableStoreCount > 0)
        {
            Input.StoreCount = Math.Clamp(Input.StoreCount, 1, AvailableStoreCount);
            return;
        }

        Input.StoreCount = 1;
    }
    public async Task<IActionResult> OnPostRecalculateCacheAsync()
    {
        try
        {
            Logs.Add("🔄 Запуск полного пересчета матрицы расстояний в Redis...");
            
            _redis.ClearCache();
            
            // Запускаем полную предзагрузку всех расстояний в Redis
            await _preloadService.PreloadAllDistancesAsync(CancellationToken.None);
        
            Logs.Add("✅ Пересчет завершен успешно");
        
            // Получаем статистику после пересчета
            var stats = await _preloadService.GetCacheStatisticsAsync();
            Logs.Add($"📊 Статистика: {stats.CachedPairs}/{stats.TotalPairs} ({stats.CachePercentage:F2}%) закешировано");
        }
        catch (Exception ex)
        {
            Logs.Add($"❌ Ошибка пересчета кеша: {ex.Message}");
            Logs.Add($"Stack: {ex.StackTrace}");
        }
    
        // Восстанавливаем данные формы
        await EnsurePlansExistAsync(Input.TestDate);
        await LoadAvailableStoreCountAsync(Input.TestDate);
    
        return Page();
    }
    
    public async Task<IActionResult> OnPostReloadCacheAsync()
    {
        try
        {
            Logs.Add("🔄 Запуск проверки полноты матрицы расстояний в Redis...");
            
            // Запускаем полную предзагрузку всех расстояний в Redis
            await _preloadService.PreloadAllDistancesAsync(CancellationToken.None);
        
            Logs.Add("✅ Проверка завершена успешно");
        
            // Получаем статистику после пересчета
            var stats = await _preloadService.GetCacheStatisticsAsync();
            Logs.Add($"📊 Статистика: {stats.CachedPairs}/{stats.TotalPairs} ({stats.CachePercentage:F2}%) закешировано");
        }
        catch (Exception ex)
        {
            Logs.Add($"❌ Ошибка пересчета кеша: {ex.Message}");
            Logs.Add($"Stack: {ex.StackTrace}");
        }
    
        // Восстанавливаем данные формы
        await EnsurePlansExistAsync(Input.TestDate);
        await LoadAvailableStoreCountAsync(Input.TestDate);
    
        return Page();
    }


    public static string FormatTime(double hours)
    {
        var timeSpan = TimeSpan.FromHours(hours);
        var h = (int)timeSpan.TotalHours;
        var m = timeSpan.Minutes;
        return $"{h} ч {m} мин";
    }
    
    public class OptimizationRunInput
    {
        [DataType(DataType.Date)]
        public DateOnly TestDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Range(1, 1700, ErrorMessage = "Количество магазинов должно быть не меньше 1.")]
        public int StoreCount { get; set; } = 15;

        [Range(10, 1000, ErrorMessage = "Размер популяции должен быть от 10 до 1000.")]
        public int PopulationSize { get; set; } = 100;

        [Range(100, 10000, ErrorMessage = "Количество поколений должно быть от 100 до 10000.")]
        public int MaxGenerations { get; set; } = 300;

        [Range(0.0, 1.0, ErrorMessage = "Crossover rate должен быть от 0 до 1.")]
        public double CrossoverRate { get; set; } = 0.8;

        [Range(0.0, 1.0, ErrorMessage = "Mutation rate должен быть от 0 до 1.")]
        public double MutationRate { get; set; } = 0.15;

        [Range(2, 100, ErrorMessage = "Tournament size должен быть от 2 до 100.")]
        public int TournamentSize { get; set; } = 5;

        [Range(0.0, 1.0, ErrorMessage = "Vehicle mutation rate должен быть от 0 до 1.")]
        public double VehicleMutationRate { get; set; } = 0.25;

        [Range(0.0, 1.0, ErrorMessage = "Farm mutation rate должен быть от 0 до 1.")]
        public double FarmMutationRate { get; set; } = 0.2;
        public FitnessObjective FitnessObjective { get; set; } = FitnessObjective.MinimizeDistance;
        public bool CacheClear { get; set; }
        public bool DisableCache { get; set; } = false;
        public int Seed { get; set; } = 1234;
    }
}
