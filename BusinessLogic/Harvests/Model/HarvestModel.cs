namespace BusinessLogic.Harvests.Model;

public class HarvestModel
{
    public int Id { get; set; }

    public DateTime DateHarvest { get; set; }
    
    public string UnitName { get; set; }

    public double Quantity { get; set; }

    public int UserId { get; set; }

    public string UserName { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; }
}