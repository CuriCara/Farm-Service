using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("RoutePlan")]
public class RoutePlan : BaseEntity
{
    public DateOnly Date { get; set; }
    public DateTime CreationTimeUtc { get; set; }

    public int VehiclesUsed { get; set; }
    public double TotalDistanceKm { get; set; }
    public double TotalTimeHours { get; set; }
    public double ObjectiveScore { get; set; }

    public ICollection<Route> Routes { get; set; } = new List<Route>();
}