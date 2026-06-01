using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.GA;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;

namespace BusinessLogic.SubServices.Logistics.DTO;

public interface IRouteDecoder
{
    DecodingResult Decode(Chromosome chromosome);
}

public record DecodingResult
{
    public List<RouteDTO> Routes { get; init; } = new();
    public ChromosomeMetrics Metrics { get; init; } = new();
    public List<DeliveryTaskDTO>? UnassignedTasks { get; set; } = new();
    public bool IsValid { get; set; }
}