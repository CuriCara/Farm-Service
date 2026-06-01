using System.Collections.Concurrent;
using System.Net.Http.Json;
using BusinessLogic.GraphHopper.DistanceMatrix.Cache;
using BusinessLogic.GraphHopper.DistanceMatrix.Cahce;
using DataAccess.Entity.GrH;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BusinessLogic.GraphHopper.DistanceMatrix;

public class GraphHopperElementWiseMatrixProvider : IDistanceMatrixProvider
{
    private readonly HttpClient _http;
    private readonly IDistanceCache _cache;
    private readonly IServerSelector _serverSelector;
    private readonly GraphHopperConfig _config;
    private const int MAXPARALLELREQUESTS = 5;
    private readonly ConcurrentDictionary<string, Task<DistanceTime>> _inFlight = new();

    public GraphHopperElementWiseMatrixProvider(
        HttpClient http,
        IDistanceCache cache,
        IServerSelector serverSelector,
        GraphHopperConfig config)
    {
        _http = http;
        _cache = cache;
        _serverSelector = serverSelector;
        _config = config;
    }
    public async Task<(double dist, double time)> GetDistanceAsync(
        LocationPoint from,
        LocationPoint to)
    {
        Exception? lastError = null;
        var maxRetries = _config.MaxRetries;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            // Получаем следующий доступный сервер
            var baseUrl = _serverSelector.GetNextServer();
            
            var url =
                $"{baseUrl}/route" +
                $"?profile={_config.Profile}" +
                $"&point={from.ToGraphHopperString()}" +
                $"&point={to.ToGraphHopperString()}" +
                $"&calc_points=false";

            try
            {
                var response = await _http.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new Exception(
                        $"GraphHopper error {(int)response.StatusCode}: {body}");
                }

                var data = await response
                    .Content
                    .ReadFromJsonAsync<GraphHopperRouteResponse>();

                var path = data?.paths.FirstOrDefault()
                           ?? throw new Exception("No route returned");

                // Успешный запрос - помечаем сервер как здоровый
                _serverSelector.MarkServerAsHealthy(baseUrl);
                
                return (path.distance, path.time / 1000.0);
            }
            catch (Exception ex)
            {
                lastError = ex;
                
                // Помечаем сервер как недоступный при ошибке
                _serverSelector.MarkServerAsFailed(baseUrl);
                
                Console.WriteLine($"⚠️ Попытка {attempt}/{maxRetries} не удалась для сервера {baseUrl}: {ex.Message}");
                
                if (attempt == maxRetries)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }

        throw new Exception($"GraphHopper request failed for {from.Id} -> {to.Id} после {maxRetries} попыток",
            lastError);
    }
   
    //Когда работает GA используем в основном In-memory (Используется в Decoder)
    public double GetDistanceWithCache(LocationPoint from, LocationPoint to)
    {
        if (from?.Index != null && to?.Index != null && 
            _cache.TryGet(from.Index, to.Index, out var cacheValue))
        {
            return cacheValue.dist / 1000.0;
        }
    
        return CalculateHaversineDistance(from, to);
    }
    
    
    //Предзагрузка матрицы расстояния из кеша и при необходимости пересчет расстояния при отсутствии записи в кеше
    //Используется в DeliveryOptimizationService
    public async Task PreloadAsync(List<LocationPoint> points)
    {
        var semaphore = new SemaphoreSlim(MAXPARALLELREQUESTS);
        var tasks = new List<Task>();

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = 0; j < points.Count; j++)
            {
                if (i != j)
                {
                    int fromIndex = points[i].Index;
                    int toIndex = points[j].Index;

                    if (_cache.TryGet(fromIndex, toIndex, out _))
                        continue;

                    tasks.Add(PreloadPair(points[i], points[j], semaphore));
                }
            }
        }

        await Task.WhenAll(tasks);
    }
    
    private async Task PreloadPair(
        LocationPoint from,
        LocationPoint to,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();

        try
        {
            // double-check (важно при гонках)
            if (_cache.TryGet(from.Index, to.Index, out _))
                return;

            // Пропускаем если это одна и та же точка
            // if (Math.Abs(from.Latitude - to.Latitude) < 1e-6 && 
            //     Math.Abs(from.Longitude - to.Longitude) < 1e-6)
            // {
            //     // Расстояние от точки до себя = 0
            //     _cache.Set(from.Index, to.Index, 0, 0);
            //     return;
            // }

            
            var (dist, time) = await GetDistanceAsync(from, to);

            _cache.Set(from.Index, to.Index, dist, time);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Preload failed for {from.Id} -> {to.Id}: {ex.Message}");
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    //Нужно только для тестов api
    public async Task<DistanceMatrixResult> GetDistanceMatrixAsync(
        IReadOnlyList<LocationPoint> points)
    {
        int n = points.Count;

        var distances = new double[n][];
        var times = new double[n][];

        for (int i = 0; i < n; i++)
        {
            distances[i] = new double[n];
            times[i] = new double[n];
        }

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(MAXPARALLELREQUESTS);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;

                if (_cache.TryGet(i, j, out var cached))
                {
                    distances[i][j] = cached.dist;
                    times[i][j] = cached.time;
                }
                else
                {
                    tasks.Add(CalculateAndCacheAsync(i, j, points[i], points[j], distances, times, semaphore));
                }
            }
        }

        if (tasks.Any())
            await Task.WhenAll(tasks);

        return new DistanceMatrixResult
        {
            Distances = distances,
            Times = times
        };
    }

    private async Task CalculateAndCacheAsync(int i, int j, LocationPoint from, LocationPoint to,
        double[][] distances, double[][] times, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            var (dist, time) = await GetDistanceAsync(from, to);
            distances[i][j] = dist;
            times[i][j] = time;
            _cache.Set(i, j, dist, time);
        }
        finally
        {
            semaphore.Release();
        }
    }

    
    
    
    //Доп метод
     
    private double CalculateHaversineDistance(LocationPoint? from, LocationPoint? to)
    {
        if (from == null || to == null)
            return double.MaxValue;
    
        if (Math.Abs(from.Latitude - to.Latitude) < 1e-6 && 
            Math.Abs(from.Longitude - to.Longitude) < 1e-6)
            return 0;
    
        const double R = 6371.0;
    
        var lat1 = from.Latitude * Math.PI / 180.0;
        var lat2 = to.Latitude * Math.PI / 180.0;
        var dLat = (to.Latitude - from.Latitude) * Math.PI / 180.0;
        var dLon = (to.Longitude - from.Longitude) * Math.PI / 180.0;
    
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    
        return R * c;
    }
    
    //Проверка заполненности
    public DistanceMatrixResult BuildMatrixFromCache(IReadOnlyList<LocationPoint> points)
    {
        int n = points.Count;

        var distances = new double[n][];
        var times = new double[n][];

        for (int i = 0; i < n; i++)
        {
            distances[i] = new double[n];
            times[i] = new double[n];
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                {
                    distances[i][j] = 0;
                    times[i][j] = 0;
                    continue;
                }

                if (_cache.TryGet(points[i].Index, points[j].Index, out var value))
                {
                    distances[i][j] = value.dist;
                    times[i][j] = value.time;
                }
                else
                {
                    // важно: не ломаем матрицу
                    distances[i][j] = double.MaxValue;
                    times[i][j] = double.MaxValue;
                }
            }
        }

        return new DistanceMatrixResult
        {
            Distances = distances,
            Times = times
        };
    }
    
    
    // Прогреваем кеш для стабильной работы

    private static bool IsFullyCached(
        IDistanceCache cache,
        List<LocationPoint> points,
        out int missingCount)
    {
        missingCount = 0;

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = 0; j < points.Count; j++)
            {
                if (i == j)
                    continue;
                if (!cache.ExistsInMemory(points[i].Index, points[j].Index))
                    missingCount++;
            }
        }

        return missingCount == 0;
    }

    public async Task EnsureCacheReady(
        IDistanceCache cache,
        IDistanceMatrixProvider matrixProvider,
        List<LocationPoint> points)
    {
        // // загружаем из долгосрочного кеша всё в память
        // if (cache is HybridDistanceCache hybridCache)
        // {
        //     Console.WriteLine("📥 Загружаем кеш из Redis в память...");
        //     var loadedCount = await hybridCache.LoadAllFromRedisToMemoryAsync(points);
        //     Console.WriteLine($"✅ Загружено {loadedCount} записей из Redis в память");
        // }
        
        // повторяем пока кеш не заполнится полностью
        while (true)
        {
            if (IsFullyCached(cache, points, out int missing))
            {
                Console.WriteLine($" Cache fully ready. Total pairs: {points.Count * points.Count}");
                return;
            }

            Console.WriteLine($" Cache incomplete. Missing: {missing}. Preloading...");

            await matrixProvider.PreloadAsync(points);
        }
    }
    
    // Метод для построения геометрии улиц на карте
    public async Task<List<LocationPoint>> GetStreetGeometryAsync(LocationPoint from, LocationPoint to)
    {
        var baseUrl = _serverSelector.GetNextServer();
        
        var url =
            $"{baseUrl}/route" +
            $"?profile={_config.Profile}" +
            $"&point={from.ToGraphHopperString()}" +
            $"&point={to.ToGraphHopperString()}" +
            $"&calc_points=true" +
            $"&points_encoded=false";

        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _serverSelector.MarkServerAsFailed(baseUrl);
            throw new Exception($"GraphHopper error {(int)response.StatusCode}: {body}");
        }

        _serverSelector.MarkServerAsHealthy(baseUrl);
        
        var data = await response.Content.ReadFromJsonAsync<GraphHopperRouteResponse>();
        var path = data?.paths.FirstOrDefault()
                   ?? throw new Exception("No route returned");

        return path.points?.coordinates?
                   .Where(c => c.Count >= 2)
                   .Select((c, i) => new LocationPoint
                   {
                       Index = i,
                       Id = $"shape_{i}",
                       Latitude = c[1],
                       Longitude = c[0]
                   })
                   .ToList()
               ?? new List<LocationPoint>();
    }

}
