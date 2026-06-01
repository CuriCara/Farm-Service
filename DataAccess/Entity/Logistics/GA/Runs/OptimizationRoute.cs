using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity.Logistics.GA.Runs;

[Table("OptimizationRoute")]
public class OptimizationRoute : BaseEntity
{
    public int OptimizationRunId { get; set; }
    [ForeignKey("OptimizationRunId")]
    public OptimizationRun Run { get; set; }

    public int VehicleId { get; set; }

    public double DistanceKm { get; set; }
    public double TimeHours { get; set; }
    public double LoadKg { get; set; }

    public string GeometryJson { get; set; } // GeoJSON или polyline
    public string StopsJson { get; set; } = null!; // Остановки на карте
}