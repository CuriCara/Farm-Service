using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("RouteStop")]
public class RouteStop : BaseEntity
{
    public int RouteId { get; set; }
    public int? FarmId { get; set; }
    public int? StoreId { get; set; }
    public int StopIndex { get; set; }

    public StopType LocationType { get; set; }

    public DateTime? ArrivalTimeUtc { get; set; }
    public DateTime? DepartureTimeUtc { get; set; }
    public int ServiceDurationMunutes { get; set; }
    public double? Latitude { get; set; }    
    public double? Longitude { get; set; }
    
    [ForeignKey("FarmId")]
    public Farm Farm { get; set; }
    
    [ForeignKey("StoreId")]
    public Store Store { get; set; }
    
    [ForeignKey("RouteId")]
    public Route Route { get; set; }

    public ICollection<RouteStopProduct> Products { get; set; } = new List<RouteStopProduct>();
}
