using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Service.GA.DistanceMatrix;

public class GraphHopperDistanceMatrixProvider : IDistanceMatrixProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    
    public GraphHopperDistanceMatrixProvider(HttpClient httpClient, IConfiguration config)
    {
        _http = httpClient;
        _apiKey = config["GraphHopper:ApiKey"]
                  ?? throw new InvalidOperationException("GraphHopper API key is missing");
    }

    public async Task<DistanceMatrixResult> GetDistanceMatrixAsync(
        IReadOnlyList<LocationPoint> points)
    {
        var request = new GraphHopperMatrixRequest
        {
            points = points.Select(p => new List<double>
            {
                p.Longitude, //Для graphhopper сначала надо указывать долготу
                p.Latitude //а только после широту 
            }).ToList(),
            out_arrays = new() { "distances", "times" }
        };

        var url = $"https://graphhopper.com/api/1/matrix?key={_apiKey}";

        var response = await _http.PostAsJsonAsync(url, request);

        response.EnsureSuccessStatusCode();

        var matrix = await response.Content.ReadFromJsonAsync<GraphHopperMatrixResponse>();

        if (matrix == null)
            throw new Exception("GraphHopper returned empty matrix");

        int n = points.Count;

        return new DistanceMatrixResult
        {
            Distances = matrix.distances,
            Times = matrix.times
        };
    }

    private double[,] To2D(List<List<double>> src, int size)
    {
        var result = new double[size, size];
        for (int i = 0; i < size; i++)
        for (int j = 0; j < size; j++)
            result[i, j] = src?[i]?[j] ?? 0;

        return result;
    }
}
