using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;

namespace DataAccess.Entity.GA;

public class RouteDTO
{
    public int Id { get; set; }
    public int? VehicleId { get; set; }
    public int DepotId { get; set; }
    public List<RouteStopDTO> Stops { get; set; } = new();
    public double DistanceKm { get; set; }
    public double TimeHours { get; set; }
    public double TotalLoadKg { get; set; }
    public double CapacityUtilization { get; set; }
    public List<LocationPoint> StreetPath { get; set; } = new();
    
    public Entity.Route ToEntity(int routePlanId, int sequenceNumber)
    {
        return new Entity.Route
        {
            RoutePlanId = routePlanId,
            VehicleId = VehicleId,
            DepotId = DepotId,
            SequenceNumber = sequenceNumber,
            DistanceKm = DistanceKm,
            TimeHours = TimeHours,
            Stops = new List<RouteStop>()
        };
    }

    public List<RouteStop> ToStopEntities(int routeId)
    {
        return Stops
            .Select((stopDto, index) => stopDto.ToEntity(routeId, index + 1))
            .ToList();
    }
}