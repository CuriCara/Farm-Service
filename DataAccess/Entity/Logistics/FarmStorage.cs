using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("FarmStorage")]
public class FarmStorage : BaseEntity
{
    public int FarmId { get; set; }
    public int ProductId { get; set; }
    public double Quantity { get; set; }

    [ForeignKey("FarmId")]
    public Farm Farm { get; set; }

    [ForeignKey("ProductId")]
    public Product Product { get; set; }
}