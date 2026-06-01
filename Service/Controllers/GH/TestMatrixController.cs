using BusinessLogic.GraphHopper.DistanceMatrix;
using DataAccess.Entity.GrH;

namespace Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using Points_Data;

[ApiController]
[Route("api/test-matrix")]
public class TestMatrixController : ControllerBase
{
    private readonly IDistanceMatrixProvider _provider;

    public TestMatrixController(IDistanceMatrixProvider provider)
    {
        _provider = provider;
    }

    [HttpGet("small_test")]
    public async Task<IActionResult> TestSmall()
    {
        var points = new List<LocationPoint>
        {
            new LocationPoint (55.7558,37.6176 ), // Msk
            new LocationPoint (59.9343,30.3351 ),  // Spb
            new LocationPoint (55.7512,37.6184 )
        };
        points.Add(new LocationPoint (50.5436,43.4843));

        
        var result = await _provider.GetDistanceMatrixAsync(points);
        return Ok(new {
            Test = "small_test",
            Count = points.Count,
            result.Distances,
            result.Times
            });
    }

    [HttpGet("big_test")]
    public async Task<IActionResult> BigTest()
    {
        var points = TestLocation.Points;

        var result = await _provider.GetDistanceMatrixAsync(points);

        return Ok(new
        {
            Test = "Large",
            Count = points.Count,
            result.Distances,
            result.Times
        });
    }
}
