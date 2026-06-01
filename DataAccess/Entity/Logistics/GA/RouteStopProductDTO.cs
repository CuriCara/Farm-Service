namespace DataAccess.Entity.Logistics.GA;

public class RouteStopProductDTO
{
    public int ProductId { get; set; }
    public double Quantity { get; set; }
    public int? TaskId { get; set; }

    public Entity.RouteStopProduct ToEntity(int routeStopId)
    {
        return new Entity.RouteStopProduct
        {
            RouteStopId = routeStopId,
            ProductId = ProductId,
            Quantity = Quantity
        };
    }
}