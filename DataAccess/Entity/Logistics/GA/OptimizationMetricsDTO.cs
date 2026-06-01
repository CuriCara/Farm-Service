namespace DataAccess.Entity.Logistics.GA;

public class OptimizationMetricsDTO
{
    public double TotalDistance { get; set; }
    public double FuelCost { get; set; }
    public int UnfulfilledTasksCount { get; set; }
    public double UnfulfilledQuantity { get; set; }
    public int TotalVehiclesUsed { get; set; }
    public int TotalDeadlineFail { get; set; }
    public double BestFitness { get; set; }  
}