using DataAccess.Entity.Logistics.GA;

namespace BusinessLogic.SubServices.Logistics.Optimization;

public enum OperationType { Load, Unload }

public class Gene
{
    public int TaskId { get; set; }
    public OperationType Operation { get; set; }
    public DeliveryTaskDTO Task { get; }
    public int? AssignedFarmId { get; set; }
    public Gene(int taskId, OperationType operation, DeliveryTaskDTO task, int? assignedFarmId = null)
    {
        TaskId = taskId;
        Operation = operation;
        Task = task;
        AssignedFarmId = assignedFarmId;
    }

    // 🔹 Метод клонирования с сохранением AssignedFarmId
    public Gene Clone() => new(TaskId, Operation, Task, AssignedFarmId);

    // 🔹 Helper: получить актуальный FarmId (приоритет: AssignedFarmId > Task.FarmId)
    public int? GetEffectiveFarmId() => AssignedFarmId ?? Task?.FarmId;
}