using System.ComponentModel.DataAnnotations.Schema;
using DataAccess.Entity.GrH;

namespace DataAccess.Entity.Logistics.GA;


public class RouteStopDTO
{
    public int StopIndex { get; set; }                    
    public StopType LocationType { get; set; }            
    public int? FarmId { get; set; }                      
    public int? StoreId { get; set; }                     
    public LocationPoint? Location { get; set; }         
    public DateTime? ArrivalTimeUtc { get; set; }         
    public DateTime? DepartureTimeUtc { get; set; }       
    public int ServiceDurationMinutes { get; set; }       
    public List<RouteStopProductDTO> Products { get; set; } = new();
    
    [NotMapped]
    public int? LocationId => LocationType switch         
    {
        StopType.Farm => FarmId,
        StopType.Store => StoreId,
        _ => null
    };

    [NotMapped]
    public double TotalQuantity
    {
        get
        {
            return Products.Sum(p => Math.Abs(p.Quantity));
            // Общий объём груза
        }
        set { }
    }

    public Entity.RouteStop ToEntity(int routeId, int stopIndex)
    {
        return new Entity.RouteStop
        {
            RouteId = routeId,
            StopIndex = stopIndex,
            
            FarmId = LocationType == StopType.Farm ? FarmId : null,
            StoreId = LocationType == StopType.Store ? StoreId : null,
            
            LocationType = LocationType,
            
            ArrivalTimeUtc = ArrivalTimeUtc,
            DepartureTimeUtc = DepartureTimeUtc,
            ServiceDurationMunutes = ServiceDurationMinutes,
            
            Latitude = Location?.Latitude,
            Longitude = Location?.Longitude,
            
            Products = new List<Entity.RouteStopProduct>()
        };
    }}