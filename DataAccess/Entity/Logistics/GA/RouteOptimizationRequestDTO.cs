using BusinessLogic.SubServices.Logistics.GA;

namespace DataAccess.Entity.Logistics.GA;

public class RouteOptimizationRequestDTO
{
    public FitnessObjective? FitnessObjective { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public List<int> StoreIds { get; set; }
    public int DepotId { get; set; }
    public int? PopulationSize { get; set; }
    public int? MaxGenerations { get; set; }
    public double? CrossoverRate { get; set; }
    public double? MutationRate { get; set; }
    public int? TournamentSize { get; set; }
    public double? VehicleMutationRate { get; set; }
    public double? FarmMutationRate { get; set; }
    public bool? CacheClear { get; set; } 
    public bool? DisableCache { get; set; }
    public int? RandomSeed { get; set; }
}
