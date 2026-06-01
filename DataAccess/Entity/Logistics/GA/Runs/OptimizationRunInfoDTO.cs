using BusinessLogic.SubServices.Logistics.GA;

namespace DataAccess.Entity.Logistics.GA.Runs;

public class OptimizationRunInfoDTO
{
    // параметры
    public int Seed { get; set; }
    public FitnessObjective FitnessObjective { get; set; }

    public int PopulationSize { get; set; }
    public int MaxGenerations { get; set; }
    public double CrossoverRate { get; set; }
    public double MutationRate { get; set; }

    // perf
    public long ExecutionTimeMs { get; set; }
    public double AvgCpuUsage { get; set; }
    public double MaxMemoryMb { get; set; }
}