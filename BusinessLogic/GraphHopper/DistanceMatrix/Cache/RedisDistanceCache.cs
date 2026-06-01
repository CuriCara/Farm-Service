using System.Text.Json;
using BusinessLogic.GraphHopper.DistanceMatrix.Cahce;
using DataAccess.Entity.GrH;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace BusinessLogic.GraphHopper.DistanceMatrix;

public class RedisDistanceCache : IDistanceCache
{
    private readonly IDatabase _database;
    private readonly string _prefix;
    private readonly TimeSpan _ttl;
    public bool MemoryOnlyMode { get; set; }
    public int HitCount { get; private set; }
    public int MissCount { get; private set; }

    public RedisDistanceCache (IConnectionMultiplexer redis,
        IConfiguration config)
    {
        _database = redis.GetDatabase();
        _prefix = config["CacheSettings:KeyPrefix"] ?? "gh:dist:";
        _ttl = TimeSpan.FromDays(config.GetValue<int>("CacheSettings:DefaultTtlDays", 30));
    }

    public async Task<Dictionary<(int from, int to), DistanceTime>> GetBatchAsync(List<(int from, int to)> pairs)
    {
        var result = new Dictionary<(int from, int to), DistanceTime>();
        var keys = pairs.Select(p => (RedisKey)$"{_prefix}{p.from}:{p.to}").ToArray();
    
        // Пакетная загрузка
        var values = await _database.StringGetAsync(keys);
    
        for (int i = 0; i < pairs.Count; i++)
        {
            if (values[i].HasValue)
            {
                try
                {
                    var value = JsonSerializer.Deserialize<DistanceTime>(values[i]!);
                    result[pairs[i]] = value;
                }
                catch { }
            }
        }
    
        return result;
    }

    public bool TryGet(int from, int to, out DistanceTime value)
    {
        var key = $"{_prefix}{from}:{to}";
        var json = _database.StringGet(key);

        if (json.HasValue)
        {
            try
            {
                //Используем record, потому что есть проблемы с сериализацией/десериализацией 
                value = JsonSerializer.Deserialize<DistanceTime>(json!)!;
            
                // Проверяем, не нулевое ли значение
                if (value.dist == 0 && value.time == 0 && from != to)
                {
                    // Нулевое значение удаляем из кеша
                    _database.KeyDelete(key);
                    MissCount++;
                    value = default;
                    return false;
                }
            
                HitCount++;
                return true;
            }
            catch (JsonException)
            {
                _database.KeyDelete(key);
                MissCount++;
                value = default;
                return false;
            }
        }

        MissCount++;
        value = default;
        return false;
    }
    
    public void Set(int from, int to, double dist, double time)
    {
        var key = $"{_prefix}{from}:{to}";
        var json = JsonSerializer.Serialize(new DistanceTime(dist, time));
        _database.StringSet(key, json, _ttl);
    }
    
    public void ClearCache()
    {
        // Получаем сервер
        var endpoint = _database.Multiplexer.GetEndPoints().First();
        var server = _database.Multiplexer.GetServer(endpoint);

        // Удаляем все ключи по префиксу
        foreach (var key in server.Keys(pattern: _prefix + "*"))
        {
            _database.KeyDelete(key);
        }

        HitCount = 0;
        MissCount = 0;
        Console.WriteLine($"[Redis] Кеш по префиксу {_prefix} полностью очищен");
    }

    public bool ExistsInMemory(int from, int to)
    {
        return true;
    }
    
    public Task<int> LoadAllFromRedisToMemoryAsync(List<LocationPoint> points)
    {
        // Redis не использует память, возвращаем 0
        return Task.FromResult(0);
    }
}

