using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Vehicle")]
public class Vehicle : BaseEntity
{
    public string Name { get; set; }
    public double Capacity { get; set; } = 3000.0;
    public int SpeedKmph { get; set; } = 60;
    public int StartPointId { get; set; } 
    public bool IsActive { get; set; }
    
    public double CostPerKm { get; set; } = 15.0;
    
    [ForeignKey("StartPointId")]
    public Farm StartDepot { get; set; }
}