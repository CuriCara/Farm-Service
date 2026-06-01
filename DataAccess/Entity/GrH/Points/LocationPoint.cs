namespace DataAccess.Entity.GrH;

public class LocationPoint
{
    public int Index { get; set; }
    public string? Id { get; set; }         
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public override string ToString()
        => $"[{Index}] {Latitude}, {Longitude}";

    public LocationPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
    
    public LocationPoint(int index, string id, double latitude, double longitude)
    {
        Index = index;
        Id = id;
        Latitude = latitude;
        Longitude = longitude;
    }
    
    public LocationPoint() { }
}