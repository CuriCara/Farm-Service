namespace DataAccess.Entity;

public class VehicleInfo
{
    public int Id { get; set; }
    public int DepotId { get; set; }
    public double Capacity { get; set; } = 5000;
    public double CostPerKm { get; set; } = 15;
    public int SpeedKmph { get; set; } = 60;
    public bool IsActive { get; set; } = true;
}