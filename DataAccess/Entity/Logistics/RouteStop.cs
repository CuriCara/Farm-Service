using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("RouteStop")]
public class RouteStop : BaseEntity
{
    public int RouteId { get; set; }
    public int StopIndex { get; set; }

    public string LocationType { get; set; }
    public int LocationId { get; set; }

    public DateTime? ArrivalTimeUtc { get; set; }
    public DateTime? DepartureTimeUtc { get; set; }

    [ForeignKey("RouteId")]
    public Route Route { get; set; }

    public ICollection<RouteStopProduct> Products { get; set; } = new List<RouteStopProduct>();
}