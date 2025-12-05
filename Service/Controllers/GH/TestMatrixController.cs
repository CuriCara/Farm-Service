namespace Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using GA.DistanceMatrix;

[ApiController]
[Route("api/test-matrix")]
public class TestMatrixController : ControllerBase
{
    private readonly IDistanceMatrixProvider _provider;

    public TestMatrixController(IDistanceMatrixProvider provider)
    {
        _provider = provider;
    }

    [HttpGet]
    public async Task<IActionResult> Test()
    {
        var points = new List<LocationPoint>
        {
            new LocationPoint { Latitude = 55.7558, Longitude = 37.6176 }, // Msk
            new LocationPoint { Latitude = 59.9343, Longitude = 30.3351 },  // Spb
            new LocationPoint { Latitude = 55.7512, Longitude = 37.6184 }
        };
        points.Add(new LocationPoint{Latitude = 50.5436, Longitude = 43.4843});

        
        var result = await _provider.GetDistanceMatrixAsync(points);
        return Ok(result);
    }
}
