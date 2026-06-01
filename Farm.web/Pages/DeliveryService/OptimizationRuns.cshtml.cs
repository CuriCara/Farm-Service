using System.Text.Json;
using DataAccess;
using DataAccess.Entity;
using DataAccess.Entity.Logistics.GA;
using DataAccess.Entity.Logistics.GA.Runs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Pages.OptimizationRuns;

public class IndexModel : PageModel
{
    private readonly FarmDbContext _db;

    public IndexModel(FarmDbContext db)
    {
        _db = db;
    }

    public List<OptimizationRun> Runs { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SelectedRunId { get; set; }

    public OptimizationRun? SelectedRun { get; set; }
    public List<RouteMapDto> Routes { get; set; } = new();

    public async Task OnGetAsync()
    {
        Runs = await _db.OptimizationRuns
            .OrderByDescending(r => r.CreationTime)
            .ToListAsync();

        if (SelectedRunId.HasValue)
        {
            SelectedRun = await _db.OptimizationRuns
                .Include(r => r.Routes)
                .FirstOrDefaultAsync(r => r.Id == SelectedRunId);

            if (SelectedRun != null)
            {
                foreach (var route in SelectedRun.Routes)
                {
                    var street = string.IsNullOrEmpty(route.GeometryJson)
                        ? new List<LocationPointDto>()
                        : JsonSerializer.Deserialize<List<LocationPointDto>>(route.GeometryJson);

                    var stops = string.IsNullOrEmpty(route.StopsJson)
                        ? new List<RouteStopsDTO>()
                        : JsonSerializer.Deserialize<List<RouteStopsDTO>>(route.StopsJson);

                    Routes.Add(new RouteMapDto
                    {
                        Id = route.Id,
                        VehicleId = route.VehicleId,
                        StreetPath = street ?? new(),
                        Stops = stops ?? new()
                    });
                }
            }
        }
    }

    public class LocationPointDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    
    public class RouteMapDto
    {
        public int Id { get; set; }
        public int? VehicleId { get; set; }
        public List<LocationPointDto> StreetPath  { get; set; } = new();
        public List<RouteStopsDTO> Stops { get; set; } = new();
    }
    
    public class RouteStopsDTO
    {
        public int StopIndex { get; set; }
        public StopType LocationType { get; set; }

        public int? FarmId { get; set; }
        public int? StoreId { get; set; }

        public LocationPointDto? Location { get; set; }
    }
}