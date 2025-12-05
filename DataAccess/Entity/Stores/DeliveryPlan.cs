using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;


[Table("DeliveryPlan")]
public class DeliveryPlan : BaseEntity
{
    public int StoreId { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public bool IsCompleted { get; set; } = false;
    
    [ForeignKey("StoreId")]
    public Store Store { get; set; }
    public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
}