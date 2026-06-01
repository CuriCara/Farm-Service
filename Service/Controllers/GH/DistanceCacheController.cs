using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.GraphHopper.DistanceMatrix.Cache;
using DataAccess;
using DataAccess.Entity.GrH;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Service.Controllers.GH;

// Контроллер для управления кешем матрицы расстояний
[ApiController]
[Route("api/distance-cache")]
public class DistanceCacheController : ControllerBase
{
    private readonly DistanceMatrixPreloadService _preloadService;
    private readonly ILogger<DistanceCacheController> _logger;
    private readonly FarmDbContext _dbContext;
    private readonly RedisDistanceCache _cache;

    public DistanceCacheController(
        DistanceMatrixPreloadService preloadService,
        ILogger<DistanceCacheController> logger,
        FarmDbContext dbContext,
        RedisDistanceCache cache)
    {
        _preloadService = preloadService;
        _logger = logger;
        _dbContext = dbContext;
        _cache = cache;
    }
    
    [HttpGet("diagnostics")]
    public async Task<IActionResult> GetDiagnostics()
    {
        var farms = await _dbContext.Farms.ToListAsync();
        var stores = await _dbContext.Stores.ToListAsync();
        
        var farmIds = farms.Select(f => f.Id).OrderBy(x => x).ToList();
        var storeIds = stores.Select(s => s.Id).OrderBy(x => x).ToList();
        
        var diagnostics = new
        {
            Farms = new
            {
                Count = farms.Count,
                MinId = farmIds.FirstOrDefault(),
                MaxId = farmIds.LastOrDefault(),
                Ids = farmIds,
                Coordinates = farms.OrderBy(f => f.Id).Select(f => new
                {
                    Id = f.Id,
                    Name = f.Name,
                    Latitude = f.Latitude,
                    Longitude = f.Longitude
                }).ToList()
            },
            Stores = new
            {
                Count = stores.Count,
                MinId = storeIds.FirstOrDefault(),
                MaxId = storeIds.LastOrDefault(),
                FirstTen = storeIds.Take(10).ToList(),
                LastTen = storeIds.TakeLast(10).ToList()
            },
            ExpectedIndices = new
            {
                FarmIndices = farmIds.Select(id => LocationIdFactory.FromFarm(id)).ToList(),
                StoreIndicesFirst10 = storeIds.Take(10).Select(id => LocationIdFactory.FromStore(id)).ToList(),
                StoreIndicesLast10 = storeIds.TakeLast(10).Select(id => LocationIdFactory.FromStore(id)).ToList()
            },
            ExpectedPairs = new
            {
                TotalPoints = farms.Count + stores.Count,
                TotalPairs = (farms.Count + stores.Count) * (farms.Count + stores.Count - 1),
                FarmToFarm = farms.Count * (farms.Count - 1),
                FarmToStore = farms.Count * stores.Count,
                StoreToFarm = stores.Count * farms.Count,
                StoreToStore = stores.Count * (stores.Count - 1)
            },
            Redis = new
            {
                ActualKeys = 93790,
                Missing = (farms.Count + stores.Count) * (farms.Count + stores.Count - 1) - 93790
            }
        };
        
        return Ok(diagnostics);
    }

    [HttpGet("test-pair")]
    public async Task<IActionResult> TestPair([FromQuery] int fromFarmId, [FromQuery] int toStoreId)
    {
        try
        {
            var farm = await _dbContext.Farms.FindAsync(fromFarmId);
            var store = await _dbContext.Stores.FindAsync(toStoreId);
            
            if (farm == null || store == null)
                return NotFound("Farm or Store not found");
            
            var fromPoint = new LocationPoint(
                index: LocationIdFactory.FromFarm(farm.Id),
                id: $"farm_{farm.Id}",
                latitude: farm.Latitude,
                longitude: farm.Longitude
            );
            
            var toPoint = new LocationPoint(
                index: LocationIdFactory.FromStore(store.Id),
                id: $"store_{store.Id}",
                latitude: store.Latitude,
                longitude: store.Longitude
            );
            
            _logger.LogInformation($"Testing route: {fromPoint.Id} -> {toPoint.Id}");
            
            // Проверяем кеш
            bool inCache = _cache.TryGet(fromPoint.Index, toPoint.Index, out var cachedValue);
            
            return Ok(new
            {
                From = new { farm.Id, farm.Name, farm.Latitude, farm.Longitude, Index = fromPoint.Index },
                To = new { store.Id, store.Name, store.Latitude, store.Longitude, Index = toPoint.Index },
                InCache = inCache,
                CachedValue = inCache ? new { cachedValue.dist, cachedValue.time } : null,
                Message = inCache ? "Found in cache" : "Not in cache - would need to fetch from GraphHopper"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing pair");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
    public async Task<IActionResult> CheckMissing()
    {
        var farms = await _dbContext.Farms.ToListAsync();
        var stores = await _dbContext.Stores.ToListAsync();
        
        var allPoints = new List<LocationPoint>();
        
        foreach (var farm in farms)
        {
            allPoints.Add(new LocationPoint(
                index: LocationIdFactory.FromFarm(farm.Id),
                id: $"farm_{farm.Id}",
                latitude: farm.Latitude,
                longitude: farm.Longitude
            ));
        }
        
        foreach (var store in stores)
        {
            allPoints.Add(new LocationPoint(
                index: LocationIdFactory.FromStore(store.Id),
                id: $"store_{store.Id}",
                latitude: store.Latitude,
                longitude: store.Longitude
            ));
        }
        
        var missing = new List<string>();
        var cached = 0;
        var missingBySource = new Dictionary<string, int>();
        
        // Проверяем все пары и группируем по источнику
        foreach (var from in allPoints)
        {
            int missingFromThisPoint = 0;
            
            foreach (var to in allPoints)
            {
                if (from.Index == to.Index) continue;
                
                if (_cache.TryGet(from.Index, to.Index, out _))
                {
                    cached++;
                }
                else
                {
                    missingFromThisPoint++;
                    if (missing.Count < 100)
                    {
                        missing.Add($"{from.Id} ({from.Index}) -> {to.Id} ({to.Index})");
                    }
                }
            }
            
            if (missingFromThisPoint > 0)
            {
                missingBySource[from.Id] = missingFromThisPoint;
            }
        }
        
        return Ok(new
        {
            TotalPoints = allPoints.Count,
            TotalPairs = allPoints.Count * (allPoints.Count - 1),
            CachedPairs = cached,
            MissingPairs = allPoints.Count * (allPoints.Count - 1) - cached,
            MissingBySource = missingBySource.OrderByDescending(x => x.Value).Take(20),
            First100Missing = missing
        });
    }


    // Получить статистику кеша расстояний
    [HttpGet("statistics")]
    public async Task<ActionResult<CacheStatistics>> GetStatistics()
    {
        try
        {
            var stats = await _preloadService.GetCacheStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статистики кеша");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Запустить предзагрузку всей матрицы расстояний
    [HttpPost("preload")]
    public async Task<ActionResult> PreloadAll()
    {
        try
        {
            _logger.LogInformation("🚀 Запуск ручной предзагрузки матрицы расстояний через API");
            
            await _preloadService.PreloadAllDistancesAsync();
            
            var stats = await _preloadService.GetCacheStatisticsAsync();
            
            return Ok(new
            {
                message = "Предзагрузка завершена успешно",
                statistics = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при предзагрузке кеша");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Альтернативный endpoint для предзагрузки
    [HttpPost("reload")]
    public async Task<ActionResult> ReloadCache()
    {
        try
        {
            _logger.LogInformation("🚀 Запуск перезагрузки кеша через API");
            
            await _preloadService.PreloadAllDistancesAsync();
            
            var stats = await _preloadService.GetCacheStatisticsAsync();
            
            return Ok(new
            {
                message = "Перезагрузка завершена успешно",
                statistics = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при перезагрузке кеша");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Очистить весь кеш расстояний
    [HttpDelete("clear")]
    public ActionResult ClearCache()
    {
        try
        {
            _logger.LogWarning("🗑️ Запуск очистки кеша через API");
            
            _preloadService.ClearCache();
            
            return Ok(new { message = "Кеш успешно очищен" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке кеша");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Проверить здоровье кеша
    [HttpGet("health")]
    public async Task<ActionResult> GetHealth()
    {
        try
        {
            var stats = await _preloadService.GetCacheStatisticsAsync();
            
            var isHealthy = stats.CachePercentage >= 95.0;
            var status = isHealthy ? "healthy" : "degraded";
            
            return Ok(new
            {
                status,
                isHealthy,
                cachePercentage = stats.CachePercentage,
                cachedPairs = stats.CachedPairs,
                missingPairs = stats.MissingPairs,
                totalPairs = stats.TotalPairs,
                farms = stats.TotalFarms,
                stores = stats.TotalStores
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке здоровья кеша");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
