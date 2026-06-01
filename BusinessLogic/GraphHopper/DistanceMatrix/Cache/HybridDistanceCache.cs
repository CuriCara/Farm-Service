using BusinessLogic.GraphHopper.DistanceMatrix.Cahce;
using DataAccess.Entity.GrH;
using Microsoft.Extensions.Caching.Memory;

namespace BusinessLogic.GraphHopper.DistanceMatrix.Cache;

public class HybridDistanceCache : IDistanceCache
{
    private readonly IMemoryCache _memory;
    private readonly RedisDistanceCache _redis;

    public bool MemoryOnlyMode { get; set; } = false;

    public HybridDistanceCache(IMemoryCache memory, RedisDistanceCache redis)
    {
        _memory = memory;
        _redis = redis;
    }

    private string Key(int from, int to) => $"{from}:{to}";

    public bool TryGet(int from, int to, out DistanceTime value)
    {
        var key = Key(from, to);

        // Всегда сначала память
        if (_memory.TryGetValue(key, out value))
            return true;

        // В режиме GA — НИКАКОГО Redis
        if (MemoryOnlyMode)
        {
            value = default;
            return false;
        }

        // Только в preload режиме
        if (_redis.TryGet(from, to, out value))
        {
            _memory.Set(key, value);
            return true;
        }

        value = default;
        return false;
    }

    public void Set(int from, int to, double dist, double time)
    {
        var val = new DistanceTime(dist, time);

        _memory.Set(Key(from, to), val);
        _redis.Set(from, to, dist, time);
    }

    public bool ExistsInMemory(int from, int to)
    {
        return _memory.TryGetValue(Key(from, to), out _);
    }

    public void ClearCache()
    {
        _redis.ClearCache();

        if (_memory is MemoryCache memCache)
            memCache.Compact(1.0);
    }
    
    // Загружает все данные из Redis в память
    public async Task<int> LoadAllFromRedisToMemoryAsync(List<LocationPoint> points)
    {
        int loadedCount = 0;
        var pairs = new List<(int from, int to)>();
    
        // Собираем все пары которые нужно загрузить
        foreach (var from in points)
        {
            foreach (var to in points)
            {
                if (from.Index == to.Index)
                    continue;
            
                var key = Key(from.Index, to.Index);
            
                if (!_memory.TryGetValue(key, out DistanceTime _))
                {
                    pairs.Add((from.Index, to.Index));
                }
            }
        }
    
        // Загружаем пакетами по 1000
        var batchSize = 1000;
        for (int i = 0; i < pairs.Count; i += batchSize)
        {
            var batch = pairs.Skip(i).Take(batchSize).ToList();
            var results = await _redis.GetBatchAsync(batch);
        
            foreach (var kvp in results)
            {
                _memory.Set(Key(kvp.Key.from, kvp.Key.to), kvp.Value);
                loadedCount++;
            }
        
            // Небольшая пауза между пакетами
            if (i + batchSize < pairs.Count)
                await Task.Delay(10);
        }
    
        return loadedCount;
    }
    
    public int GetMemoryCacheCount(List<LocationPoint> points)
    {
        int count = 0;
        foreach (var from in points)
        {
            foreach (var to in points)
            {
                if (from.Index == to.Index) continue;
                if (ExistsInMemory(from.Index, to.Index))
                    count++;
            }
        }
        return count;
    }
}