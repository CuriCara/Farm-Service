using BusinessLogic.SubServices.Logistics.Optimization;

namespace DataAccess.Entity.Logistics.GA.Chromosome;

public sealed class RouteTaskPair
{
    public required Gene Load { get; init; }
    public required Gene Unload { get; init; }
}