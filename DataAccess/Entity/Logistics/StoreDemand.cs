using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("StoreDemand")]
public class StoreDemand : BaseEntity
{
    public int StoreId { get; set; }

    [ForeignKey(nameof(StoreId))]
    public Store Store { get; set; }
    public DateOnly Date { get; set; }
    public int ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; }
    public double RequiredQuantity { get; set; }
    public double PlannedQuantity { get; set; }
    
    [NotMapped]
    public double RemainingQuantity => RequiredQuantity - PlannedQuantity;
}