namespace Service.GA.DistanceMatrix;

public class GraphHopperMatrixRequest
{
    public List<List<double>> points { get; set; } = new();
    public List<string> out_arrays { get; set; } = new() { "distances", "times" };
}