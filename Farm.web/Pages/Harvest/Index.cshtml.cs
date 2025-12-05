using Microsoft.AspNetCore.Mvc.RazorPages;
using AutoMapper;
using BusinessLogic.Harvests.Model;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Provider;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using DataAccess.Entity;
using Microsoft.AspNetCore.Authorization;

[Authorize]
public class IndexModelH : PageModel
{
    [TempData]
    public string? ErrorMessage { get; set; }
    private readonly HarvestManager _harvestManager;
    private readonly IRepository<Product> _productRepository;
    private readonly IMapper _mapper;
    private readonly FarmProvider _farmProvider;

    [BindProperty(SupportsGet = true)]
    public HarvestFilterModel Filter { get; set; } = new();

    public IndexModelH(HarvestManager harvestManager, IRepository<Product> productRepository, IMapper mapper, FarmProvider farmProvider)
    {
        _harvestManager = harvestManager;
        _productRepository = productRepository;
        _farmProvider = farmProvider;
        _mapper = mapper;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var products = await _productRepository.GetAllAsync();
        Filter.Products = products
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.ProductName })
            .ToList();
        var farms = await _farmProvider.GetAllAsync();
        Filter.Farms = farms
            .Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name })
            .ToList();
        
        var allHarvests = await _harvestManager.GetAllAsync();
        var mapped = _mapper.Map<List<HarvestModel>>(allHarvests);

        var filtered = mapped.AsQueryable();

        if (Filter.FromDate != null)
            filtered = filtered.Where(h => h.DateHarvest >= Filter.FromDate.Value);

        if (Filter.ToDate != null)
            filtered = filtered.Where(h => h.DateHarvest <= Filter.ToDate.Value);

        if (Filter.ProductId.HasValue)
            filtered = filtered.Where(h => h.ProductId == Filter.ProductId.Value);

        if (Filter.FarmId.HasValue)
            filtered = filtered.Where(h => h.FarmId == Filter.FarmId.Value);

        Filter.Results = filtered.ToList();

        return Page();
    }
    
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!IsCurrentUserAdmin())
        {
            return RedirectToPage("/Account/AccessDenied");
        }
        
        var harvest = await _harvestManager.GetByIdAsync(id);
        if (harvest != null)
        {
            await _harvestManager.DeleteAsync(harvest);
        }
        return RedirectToPage();
    }

    public bool IsCurrentUserAdmin()
    {
        return User.IsInRole("Admin");
    }
}
