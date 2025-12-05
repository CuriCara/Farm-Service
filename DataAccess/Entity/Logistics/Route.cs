using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Route")]
public class Route : BaseEntity
{
    public int RoutePlanId { get; set; }
    public int? VehicleId { get; set; } 
    public int DepotId { get; set; }

    public double DistanceKm { get; set; }
    public double TimeHours { get; set; }
    public int SequenceNumber { get; set; }

    [ForeignKey("RoutePlanId")]
    public RoutePlan RoutePlan { get; set; }

    [ForeignKey("VehicleId")]
    public Vehicle Vehicle { get; set; }

    [ForeignKey("DepotId")]
    public Farm Depot { get; set; }

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}