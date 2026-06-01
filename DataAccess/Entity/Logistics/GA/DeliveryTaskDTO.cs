using DataAccess.Entity.GrH;

namespace DataAccess.Entity.Logistics.GA;

public class DeliveryTaskDTO
{
    public int Id { get; set; }
    public int? FarmId { get; set; }
    public int StoreId { get; set; }
    public int ProductId { get; set; }
    public double Quantity { get; set; }
    public LocationPoint StoreCoord { get; set; }
    public double Priority { get; set; } = 1.0;
    public TimeSpan? TimeWindowOpen { get; set; } = TimeSpan.FromHours(9);
    public TimeSpan? TimeWindowClose { get; set; } = TimeSpan.FromHours(18);
    public bool IsShortage { get; set; } = false;

    public override bool Equals(object? obj)
    {
        return obj is DeliveryTaskDTO other && this.Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}