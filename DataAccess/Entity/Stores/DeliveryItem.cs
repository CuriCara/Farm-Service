using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("DeliveryItem")]
public class DeliveryItem : BaseEntity
{
    public int DeliveryPlanId { get; set; }
    public int ProductId { get; set; }
    public double Quantity { get; set; }
    
    public DeliveryPlan DeliveryPlan { get; set; }
    public Product Product { get; set; }
}