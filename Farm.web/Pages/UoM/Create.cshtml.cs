using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

[Authorize(Roles = "Admin")]
public class CreateUoMModel : PageModel
{
    private readonly IRepository<UnitsOfMeasurement> _uoMRepo;
    private readonly UnitCategoryProvider _categoryProvider;

    public SelectList UnitCategoryOptions { get; set; }

    [BindProperty]
    public UnitsOfMeasurement UoM { get; set; }

    public CreateUoMModel(IRepository<UnitsOfMeasurement> uoMRepo, UnitCategoryProvider categoryProvider)
    {
        _uoMRepo = uoMRepo;
        _categoryProvider = categoryProvider;
    }

    public async Task OnGetAsync()
    {
        var categories = await _categoryProvider.GetAllAsync();
        UnitCategoryOptions = new SelectList(categories, "Id", "Name");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();

        if (!ModelState.IsValid)
            return Page();

        var existing = await _uoMRepo.GetAllAsync();
        if (existing.Any(x => x.UoM.ToLower() == UoM.UoM.ToLower() && x.UnitCategoryId == UoM.UnitCategoryId))
        {
            ModelState.AddModelError("UoM.UoM", "Такая единица уже существует в этой категории.");
            return Page();
        }

        await _uoMRepo.AddAsync(UoM);
        
        var uomWithCategory = (await _uoMRepo.GetAllAsync())
            .FirstOrDefault(x => x.Id == UoM.Id);

        return RedirectToPage("/Harvest/Index");
    }
}