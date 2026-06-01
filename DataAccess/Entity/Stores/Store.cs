using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Store")]
public class Store : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = default;
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    
    public TimeSpan? OpeningTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }
    public int DefaultServiceDurationMinutes { get; set; } = 30;
    
    public ICollection<DeliveryPlan> DeliveryPlans { get; set; } = new List<DeliveryPlan>();
    public ICollection<StoreProduct> Products { get; set; } = new List<StoreProduct>();
    public ICollection<StoreDemand> Demands { get; set; } = new List<StoreDemand>();
    public ICollection<RouteStop> RouteStops { get; set; } = new List<RouteStop>();
}