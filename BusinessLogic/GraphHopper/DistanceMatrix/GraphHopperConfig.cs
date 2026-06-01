namespace BusinessLogic.GraphHopper.DistanceMatrix;

// Конфигурация для GraphHopper серверов
public class GraphHopperConfig
{
    // Список URL серверов GraphHopper для балансировки нагрузки
    public List<string> Servers { get; set; } = new();
    
    // Профиль маршрутизации
    public string Profile { get; set; } = "car";
    
    // Таймаут запроса в секундах
    public int RequestTimeoutSeconds { get; set; } = 30;
    
    // Максимальное количество попыток при ошибке
    public int MaxRetries { get; set; } = 3;
}
