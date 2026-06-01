using BusinessLogic.Harvests.Provider;
using DataAccess.Entity.GrH;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.GraphHopper.DistanceMatrix.Cache;

// Сервис для предзагрузки матрицы расстояний между фермами и магазинами
public class DistanceMatrixPreloadService
{
    private readonly IDistanceMatrixProvider _matrixProvider;
    private readonly IDistanceCache _cache;
    private readonly FarmProvider _farmProvider;
    private readonly StoreProvider _storeProvider;
    private readonly ILogger<DistanceMatrixPreloadService> _logger;

    public DistanceMatrixPreloadService(
        IDistanceMatrixProvider matrixProvider,
        IDistanceCache cache,
        FarmProvider farmProvider,
        StoreProvider storeProvider,
        ILogger<DistanceMatrixPreloadService> logger)
    {
        _matrixProvider = matrixProvider;
        _cache = cache;
        _farmProvider = farmProvider;
        _storeProvider = storeProvider;
        _logger = logger;
    }
    
    // Предзагружает всю матрицу расстояний между фермами и магазинами
    public async Task PreloadAllDistancesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Начинаем предзагрузку матрицы расстояний...");
        var startTime = DateTime.UtcNow;

        try
        {
            // Получаем все фермы и магазины
            var farms = await _farmProvider.GetAllAsync();
            var stores = await _storeProvider.GetAllAsync();

            _logger.LogInformation($"Найдено ферм: {farms.Count}, магазинов: {stores.Count}");

            // Преобразуем в LocationPoint
            var allPoints = new List<LocationPoint>();
            
            // Добавляем фермы
            int index = 0;
            foreach (var farm in farms)
            {
                allPoints.Add(new LocationPoint(
                    index: LocationIdFactory.FromFarm(farm.Id),
                    id: $"farm_{farm.Id}",
                    latitude: farm.Latitude,
                    longitude: farm.Longitude
                ));
            }

            // Добавляем магазины
            foreach (var store in stores)
            {
                allPoints.Add(new LocationPoint(
                    index: LocationIdFactory.FromStore(store.Id),
                    id: $"store_{store.Id}",
                    latitude: store.Latitude,
                    longitude: store.Longitude
                ));
            }

            _logger.LogInformation($"Всего точек для кеширования: {allPoints.Count}");

            // Подсчитываем количество пар
            int totalPairs = allPoints.Count * (allPoints.Count - 1);
            _logger.LogInformation($"Всего пар для расчета: {totalPairs:N0}");

            // Проверяем, сколько уже закешировано
            int cachedCount = 0;
            int missingCount = 0;

            for (int i = 0; i < allPoints.Count; i++)
            {
                for (int j = 0; j < allPoints.Count; j++)
                {
                    if (i == j) continue;

                    if (_cache.TryGet(allPoints[i].Index, allPoints[j].Index, out _))
                        cachedCount++;
                    else
                        missingCount++;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Предзагрузка отменена пользователем");
                    return;
                }
            }

            _logger.LogInformation($"Уже в кеше: {cachedCount:N0} ({cachedCount * 100.0 / totalPairs:F2}%)");
            _logger.LogInformation($"Отсутствует: {missingCount:N0} ({missingCount * 100.0 / totalPairs:F2}%)");

            if (missingCount == 0)
            {
                _logger.LogInformation("Все расстояния уже закешированы!");
                return;
            }

            // Загружаем недостающие расстояния
            _logger.LogInformation($"Начинаем загрузку {missingCount:N0} недостающих расстояний...");
            
            await _matrixProvider.PreloadAsync(allPoints);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation($"Предзагрузка завершена за {duration.TotalMinutes:F2} минут");
            _logger.LogInformation($"Скорость: {totalPairs / duration.TotalSeconds:F2} пар/сек");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при предзагрузке матрицы расстояний");
            throw;
        }
    }
    
    // Получает статистику кеша
    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        var farms = await _farmProvider.GetAllAsync();
        var stores = await _storeProvider.GetAllAsync();

        var allPoints = new List<LocationPoint>();
        int index = 0;

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

        int totalPairs = allPoints.Count * (allPoints.Count - 1);
        int cachedCount = 0;

        for (int i = 0; i < allPoints.Count; i++)
        {
            for (int j = 0; j < allPoints.Count; j++)
            {
                if (i == j) continue;

                try
                {
                    if (_cache.TryGet(allPoints[i].Index, allPoints[j].Index, out _))
                        cachedCount++;
                }
                catch (Exception ex)
                {
                    // Пропускаем ошибки Redis timeout
                    _logger.LogWarning($"Ошибка при проверке кеша {allPoints[i].Index}→{allPoints[j].Index}: {ex.Message}");
                }
            }
        }

        return new CacheStatistics
        {
            TotalFarms = farms.Count,
            TotalStores = stores.Count,
            TotalPoints = allPoints.Count,
            TotalPairs = totalPairs,
            CachedPairs = cachedCount,
            MissingPairs = totalPairs - cachedCount,
            CachePercentage = totalPairs > 0 ? cachedCount * 100.0 / totalPairs : 0
        };
    }
    
    // Очищает весь кеш расстояний
    public void ClearCache()
    {
        _logger.LogWarning("Очистка кеша расстояний...");
        _cache.ClearCache();
        _logger.LogInformation("Кеш очищен");
    }
}

// Статистика кеша расстояний
public class CacheStatistics
{
    public int TotalFarms { get; set; }
    public int TotalStores { get; set; }
    public int TotalPoints { get; set; }
    public int TotalPairs { get; set; }
    public int CachedPairs { get; set; }
    public int MissingPairs { get; set; }
    public double CachePercentage { get; set; }
}
