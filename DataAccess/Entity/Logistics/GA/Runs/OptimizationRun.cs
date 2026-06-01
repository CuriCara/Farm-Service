using System.ComponentModel.DataAnnotations.Schema;
using BusinessLogic.SubServices.Logistics.GA;

namespace DataAccess.Entity.Logistics.GA.Runs;

[Table("OptimizationRun")]
public class OptimizationRun : BaseEntity
{
    public DateOnly PlanningDate { get; set; }

    public int Seed { get; set; }
    public FitnessObjective FitnessObjective { get; set; }

    // параметры ГА
    public int PopulationSize { get; set; }
    public int MaxGenerations { get; set; }
    public double CrossoverRate { get; set; }
    public double MutationRate { get; set; }    
    public double MutationFarmRate { get; set; }
    public double MutationVehicleRate { get; set; }
    // результат
    public double BestFitness { get; set; }
    public double TotalDistance { get; set; }
    public double FuelCost { get; set; }

    public int TotalVehiclesUsed { get; set; }
    public int TotalTimeViolations { get; set; }
    
    public long ExecutionTimeMs { get; set; }     // общее время
    public double AvgCpuUsage { get; set; }       // средняя загрузка CPU (%)
    public long MaxMemoryMb { get; set; }         // пик памяти

    public ICollection<OptimizationRoute> Routes { get; set; } = new List<OptimizationRoute>();
    public ICollection<FitnessHistoryPoint> FitnessHistory { get; set; } = new List<FitnessHistoryPoint>();
}