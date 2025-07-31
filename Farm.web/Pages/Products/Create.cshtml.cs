using System.ComponentModel.DataAnnotations;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

[Authorize(Roles = "Admin")]
public class CreateProductModel : PageModel
{
    private readonly ProductProvider _productProvider;
    private readonly UnitCategoryProvider _categoryProvider;

    public CreateProductModel(ProductProvider productProvider, UnitCategoryProvider categoryProvider)
    {
        _productProvider = productProvider;
        _categoryProvider = categoryProvider;
    }

    [BindProperty]
    [Required]
    public string ProductName { get; set; }

    [BindProperty]
    [Required]
    public int SelectedCategoryId { get; set; }

    public List<SelectListItem> UnitCategoryOptions { get; set; }

    public async Task OnGetAsync()
    {
        var categories = await _categoryProvider.GetAllAsync();
        UnitCategoryOptions = categories
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();

        if (!ModelState.IsValid)
            return Page();
        
        var product = new Product
        {
            ProductName = ProductName,
            UnitCategoryId = SelectedCategoryId,
            ExternalId = Guid.NewGuid()
        };

        await _productProvider.AddAsync(product);
        return RedirectToPage("/Harvest/Index");
    }
}