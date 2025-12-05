using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Vehicle")]
public class Vehicle : BaseEntity
{
    public string Name { get; set; }
    public double Capacity { get; set; } 
    public int SpeedKmph { get; set; } = 60;
    public int StartPointId { get; set; } 
    public bool IsActive { get; set; }
    
    [ForeignKey("StartPointId")]
    public Farm StartDepot { get; set; }
}