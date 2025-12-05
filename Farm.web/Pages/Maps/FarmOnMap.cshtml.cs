using DataAccess;
using DataAccess.Entity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace Farm.web.Pages.Maps;


public class FarmsMapModel : PageModel
{
    private readonly FarmDbContext _context;
    public FarmsMapModel(FarmDbContext context) => _context = context;

    public List<DataAccess.Entity.Farm> Farms { get; set; } = new();
    public List<DataAccess.Entity.Store> Stores { get; set; } = new();

    public async Task OnGetAsync()
    {
        Farms = await _context.Farms.ToListAsync();
        Stores = await _context.Stores.ToListAsync();
    }
    
}
