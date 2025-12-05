using DataAccess.Entity;

public class DeliveryTask
{
    public int StoreId { get; set; }
    public int FarmId { get; set; }
    public int ProductId { get; set; }
    public double Quantity { get; set; }
    public Store Store { get; set; }
    public Farm Farm { get; set; }
}