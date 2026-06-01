using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.GA;
using DataAccess.Entity.Logistics.GA.Runs;

namespace DataAccess.Entity.Logistics.GA;

public class RouteOptimizationResultDTO
{
    public List<RouteDTO> Routes { get; set; }
    
    public OptimizationMetricsDTO Metrics { get; set; } = new();
    
    public string? Warning { get; set; }
    
    public DateOnly PlanningDate { get; set; }
    
    public List<double>? FitnessHistory { get; set; }
    
    public OptimizationRunInfoDTO RunInfo { get; set; } = new();
}