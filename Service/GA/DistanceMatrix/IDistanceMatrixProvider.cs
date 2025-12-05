namespace Service.GA.DistanceMatrix;

public interface IDistanceMatrixProvider
{
    Task<DistanceMatrixResult> GetDistanceMatrixAsync(IReadOnlyList<LocationPoint> points);
}