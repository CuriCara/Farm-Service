using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.Logistics.GA;

namespace BusinessLogic.SubServices.Logistics.GA;

public interface IGeneticAlgorithm
{
    Task<Solution> OptimizeAsync(
        List<DeliveryTaskDTO> tasks,
        CancellationToken cancellationToken = default);

    OptimizationResult GetOptimizationResult();
}