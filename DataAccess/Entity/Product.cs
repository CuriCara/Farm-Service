using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Product")]
public class Product : BaseEntity
{
    public string ProductName { get; set; }
    
    public int UnitCategoryId { get; set; }

    [ForeignKey(nameof(UnitCategoryId))]
    public UnitCategory Category { get; set; }
    
    public List<Harvest> Harvests { get; set; }
}