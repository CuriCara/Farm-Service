namespace BusinessLogic.SubServices.Logistics.GA;

public enum FitnessObjective
{
    MinimizeVehicles,      // Минимизация количества машин
    MinimizeDistance,      // Минимизация общего пробега
    MinimizeTimeViolations // Минимизация нарушений временных окон
}