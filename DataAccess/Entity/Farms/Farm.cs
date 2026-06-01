using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Farm")]
public class Farm : BaseEntity
{
    public string Name { get; set; } = default;
    public string Address { get; set; } = default;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    public TimeSpan? OpeningTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }
    public int DefaultServiceDurationMinutes { get; set; } = 30;

    public List<Harvest> Harvests { get; set; } = new();

    public ICollection<FarmStorage> Storages { get; set; } = new List<FarmStorage>();
    public ICollection<RouteStop> RouteStops { get; set; } = new List<RouteStop>();
}