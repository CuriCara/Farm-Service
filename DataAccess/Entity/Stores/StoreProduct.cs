using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("StoreProduct")]
public class StoreProduct : BaseEntity
{
    public int StoreId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    
    [ForeignKey("StoreId")]
    public Store Store { get; set; }
    [ForeignKey("ProductId")]
    public Product Product { get; set; }
}