using System.Collections.Concurrent;

namespace BusinessLogic.GraphHopper.DistanceMatrix;

// Round-Robin балансировщик с поддержкой failover
// Распределяет запросы равномерно между серверами, исключая недоступные
public class RoundRobinServerSelector : IServerSelector
{
    private readonly List<string> _servers;
    private readonly ConcurrentDictionary<string, DateTime> _failedServers = new();
    private readonly TimeSpan _recoveryTime = TimeSpan.FromMinutes(2);
    private int _currentIndex = 0;
    private readonly object _lock = new();

    public RoundRobinServerSelector(List<string> servers)
    {
        if (servers == null || servers.Count == 0)
            throw new ArgumentException("Список серверов не может быть пустым", nameof(servers));
        
        _servers = servers;
    }

    public string GetNextServer()
    {
        lock (_lock)
        {
            // Очищаем восстановленные сервера
            RecoverHealthyServers();
            
            var availableServers = _servers
                .Where(s => !_failedServers.ContainsKey(s))
                .ToList();

            if (availableServers.Count == 0)
            {
                // Все сервера недоступны - сбрасываем список и пробуем снова
                Console.WriteLine("Все GraphHopper сервера недоступны. Сброс состояния...");
                _failedServers.Clear();
                availableServers = _servers;
            }

            // Round-robin по доступным серверам
            var server = availableServers[_currentIndex % availableServers.Count];
            _currentIndex = (_currentIndex + 1) % availableServers.Count;
            
            return server;
        }
    }

    public void MarkServerAsFailed(string serverUrl)
    {
        _failedServers.TryAdd(serverUrl, DateTime.UtcNow);
        Console.WriteLine($"GraphHopper сервер помечен как недоступный: {serverUrl}");
    }

    public void MarkServerAsHealthy(string serverUrl)
    {
        if (_failedServers.TryRemove(serverUrl, out _))
        {
            Console.WriteLine($"GraphHopper сервер восстановлен: {serverUrl}");
        }
    }

    private void RecoverHealthyServers()
    {
        var now = DateTime.UtcNow;
        var recovered = _failedServers
            .Where(kvp => now - kvp.Value > _recoveryTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var server in recovered)
        {
            MarkServerAsHealthy(server);
        }
    }
}
