using System.ComponentModel.DataAnnotations;
using AutoMapper;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

[Authorize]
public class CreateModel : PageModel

{
    private readonly HarvestManager _harvestManager;
    private readonly IMapper _mapper;
    private readonly UserManager<User> _userManager;
    private readonly ProductProvider _productRepo;
    private readonly UnitsOfMeasurementProvider _uomRepo;

    [BindProperty]
    public CreateInputModel Input { get; set; }

    public SelectList ProductSelectList { get; set; }
    public SelectList UnitSelectList { get; set; }

    public CreateModel(HarvestManager harvestManager, IMapper mapper, 
        UserManager<User> userManager, ProductProvider productRepo, UnitsOfMeasurementProvider uomRepo)
    {
        _harvestManager = harvestManager;
        _mapper = mapper;
        _userManager = userManager;
        _productRepo = productRepo;
        _uomRepo = uomRepo;
    }

    public class CreateInputModel
    {
        [DataType(DataType.Date)]
        [Required]
        [Display(Name = "Дата сбора")]
        public DateTime DateHarvest { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть положительным")]
        [Display(Name = "Количество")]
        public double Quantity { get; set; }

        [Required]
        [Display(Name = "Продукт")]
        public int ProductId { get; set; }
        
        [Required]
        [Display(Name = "Единица измерения")]
        public int UnitId { get; set; }
    }

    public async Task OnGetAsync()
    {
        
        var products = await _productRepo.GetAllAsync();
        ProductSelectList = new SelectList(products, "Id", "ProductName");
        
        var units = await _uomRepo.GetAllAsync();
        UnitSelectList = new SelectList(units, "Id", "UoM");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var products = await _productRepo.GetAllAsync();
        ProductSelectList = new SelectList(products, "Id", "ProductName");
        
        var units = await _uomRepo.GetAllAsync();
        UnitSelectList = new SelectList(units, "Id", "UoM");

        if (!ModelState.IsValid)
            return Page();

        var product = await _productRepo.GetByIdAsync(Input.ProductId);
        if (product == null)
        {
            ModelState.AddModelError("", "Продукт не был найден.");
            return Page();
        }

        var unit = await _uomRepo.GetByIdAsync(Input.UnitId);
        if (unit == null)
        {
            ModelState.AddModelError("", "Единица измерения не найдена.");
            return Page();
        }
        
        if (unit.UnitCategoryId != product.UnitCategoryId)
        {
            ModelState.AddModelError("", "Единица измерения не соответствует категории продукта.");
            return Page();
        }
        
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        double baseQuantity = Input.Quantity * unit.ConversionFactor;

        var entity = new Harvest
        {
            DateHarvest = DateTime.SpecifyKind(Input.DateHarvest, DateTimeKind.Utc),
            Quantity = baseQuantity,
            ProductId = product.Id,
            UserId = user.Id,
            UnitId = unit.Id
        };

        await _harvestManager.AddAsync(entity);
        return RedirectToPage("/Harvest/Index");
    }
}