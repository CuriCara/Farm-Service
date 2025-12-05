using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("RouteStopProduct")]
public class RouteStopProduct : BaseEntity
{
    public int RouteStopId { get; set; }
    public int ProductId { get; set; }
    public double Quantity { get; set; }

    [ForeignKey("RouteStopId")]
    public RouteStop RouteStop { get; set; }

    [ForeignKey("ProductId")]
    public Product Product { get; set; }
}