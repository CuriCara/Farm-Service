namespace DataAccess.Entity.GrH;

public class GraphHopperRouteResponse
{
    public List<Path> paths { get; set; } = new();

    public class Path
    {
        public double distance { get; set; }
        public long time { get; set; }
        
        public RoutePoints? points { get; set; }
    }
    
    public class RoutePoints
    {
        public string type { get; set; } = string.Empty;
        public List<List<double>> coordinates { get; set; } = new();
    }
}