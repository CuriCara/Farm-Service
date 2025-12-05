namespace Service.GA.DistanceMatrix;

public class GraphHopperSettings
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://graphhopper.com/api/1";
    public string Profile { get; set; } = "car";
}
