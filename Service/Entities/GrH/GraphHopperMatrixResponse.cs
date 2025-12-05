namespace Service.GA.DistanceMatrix;

public class GraphHopperMatrixResponse
{
    public List<List<double>> distances { get; set; }
    public List<List<double>> times { get; set; }
}
