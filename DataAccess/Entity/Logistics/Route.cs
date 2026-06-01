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
    
    public double TotalLoadKg { get; set; }         
    public double CapacityUtilization { get; set; } 
    public int TimeWindowViolations { get; set; }   
    public double EstimatedCost { get; set; }       
    public DateTime? StartTime { get; set; }        
    public DateTime? EndTime { get; set; }    

    [ForeignKey("RoutePlanId")]
    public RoutePlan RoutePlan { get; set; }

    [ForeignKey("VehicleId")]
    public Vehicle Vehicle { get; set; }

    [ForeignKey("DepotId")]
    public Farm Depot { get; set; }

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}