namespace BusinessLogic.GraphHopper.DistanceMatrix;

// Интерфейс для выбора сервера из пула
public interface IServerSelector
{
    // Получить следующий доступный сервер
    string GetNextServer();
    
    // Пометить сервер как недоступный
    void MarkServerAsFailed(string serverUrl);
    
    // Восстановить сервер в пул доступных
    void MarkServerAsHealthy(string serverUrl);
}