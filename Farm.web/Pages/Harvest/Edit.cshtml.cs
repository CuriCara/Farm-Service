using System.ComponentModel.DataAnnotations;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
[Authorize (Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly HarvestManager _harvestManager;
    private readonly IRepository<Product> _productRepo;
    private readonly IRepository<UnitsOfMeasurement> _uomRepo;

    [BindProperty]
    public EditInputModel Input { get; set; }
    public IList<SelectListItem> Products { get; set; }
    public IList<SelectListItem> Units { get; set; }

    public EditModel(HarvestManager harvestManager, IRepository<Product> productRepo, IRepository<UnitsOfMeasurement> uomRepo)
    {
        _harvestManager = harvestManager;
        _productRepo = productRepo;
        _uomRepo = uomRepo;
    }

    public class EditInputModel
    {
        public int Id { get; set; }
        [DataType(DataType.Date)]
        [Display(Name = "Дата сбора")]
        public DateTime DateHarvest { get; set; }
        [Display(Name = "Количество")]
        public double Quantity { get; set; }
        [Display(Name = "Продукт")]
        public int ProductId { get; set; }
        [Required(ErrorMessage = "Выберите единицу измерения")]
        [Display(Name = "Единица измерения")]
        public int UnitId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/Account/Login");
        }
        
        var harvest = await _harvestManager.GetByIdAsync(id);
        if (harvest == null) return NotFound();

        var product = harvest.Product;
        if (product == null) return BadRequest("Продукт не найден");

        var units = await _uomRepo.GetAllAsync();
        var categoryUnits = units
            .Where(u => u.UnitCategoryId == product.UnitCategoryId)
            .ToList();

        var selectUnit = categoryUnits.FirstOrDefault(u =>
            Math.Abs(u.ConversionFactor * harvest.Quantity - harvest.Quantity) < 0.0001) ?? categoryUnits.First();

        Input = new EditInputModel
        {
            Id = harvest.Id,
            DateHarvest = harvest.DateHarvest,
            Quantity = harvest.Quantity / selectUnit.ConversionFactor,
            ProductId = harvest.ProductId,
            UnitId = selectUnit.Id
        };

        Products = (await _productRepo.GetAllAsync()).Select(p =>
            new SelectListItem { Value = p.Id.ToString(), Text = p.ProductName }).ToList();

        Units = categoryUnits.Select(u =>
            new SelectListItem { Value = u.Id.ToString(), Text = u.UoM }).ToList();
            
            
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        
        var harvest = await _harvestManager.GetByIdAsync(Input.Id);
        if (harvest == null) return NotFound();

        var unit = await _uomRepo.GetByIdAsync(Input.UnitId);
        if (unit == null) return BadRequest("Единица измерения не найдена");
        
        harvest.DateHarvest = DateTime.SpecifyKind(Input.DateHarvest, DateTimeKind.Utc);
        harvest.Quantity = Input.Quantity * unit.ConversionFactor;
        harvest.ProductId = Input.ProductId;
        
        await _harvestManager.UpdateAsync(harvest);
        return RedirectToPage("Index");
    }
}