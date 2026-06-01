using System.ComponentModel.DataAnnotations;
using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Farm.web.Pages.StorePage;

[Authorize(Roles = "Admin")]
public class EditDeliveryPlan : PageModel
{
    private readonly DeliveryPlanProvider _planProvider;
    private readonly ProductProvider _productProvider;
    
    public DeliveryPlan Plan { get; set; } = null!;
    public List<SelectListItem> Items { get; set; } = new();
    [BindProperty] 
    public EditDeliveryPlanModel editDeliveryPlanModel { get; set; } = new();
    [BindProperty] 
    public NewDeliveryItemModel newDeliveryItemModel { get; set; } = new();

    public EditDeliveryPlan(DeliveryPlanProvider planProvider, ProductProvider productProvider)
    {
        _planProvider = planProvider;
        _productProvider = productProvider;
    }
    
    public class EditDeliveryPlanModel
    {
        public int PlanId { get; set; }
        public List<EditDeliveryItemModel> ItemModels { get; set; } = new();
    }
    public class EditDeliveryItemModel
    {
        public int DeliveryItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Укажите количество")]
        [Range(0.001, double.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public double Quantity { get; set; }
    }
    
    public class NewDeliveryItemModel
    {
        [Required(ErrorMessage = "Выберите товар")]
        public int ProductId { get; set; }
        [Required(ErrorMessage = "Укажите количество")]
        [Range(0.001, double.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
        public double Quantity { get; set; }
    }
    
    
    public async Task<IActionResult> OnGetAsync(int PlanId)
    {
        Plan = await _planProvider.GetByIdAsync(PlanId);

        if (Plan == null)
            return NotFound();

        editDeliveryPlanModel.PlanId = Plan.Id;
        editDeliveryPlanModel.ItemModels = Plan.Items?
            .Select(item => new EditDeliveryItemModel
                {
                    DeliveryItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product?.ProductName ?? "Неизвестный товар",
                    CategoryName = item.Product?.Category?.Name ?? "null",
                    Quantity = item.Quantity
                }
            ).ToList() ?? new List<EditDeliveryItemModel>();

        var existingProductId = Plan.Items?.Select(i => i.ProductId).ToList() ?? new List<int>();
        var allProducts = await _productProvider.GetAllAsync();

        Items = allProducts
            .Where(p => !existingProductId.Contains(p.Id))
            .OrderBy(p => p.Category?.Name)
            .ThenBy(p => p.ProductName)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.Category?.Name ?? "Без категории"} - {p.ProductName}"
            })
            .ToList();
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("", "Не все данные корректны!");
            return Page();
        }

        try
        {
            foreach (var item in editDeliveryPlanModel.ItemModels)
            {
                var entity = new DeliveryPlan(
                    
                    );
                
                    //await _planProvider.UpdateAsync();
            }
        }
        catch (Exception exception)
        {
            ModelState.AddModelError("", $"Ошибка - {exception.Message}");
            return Page();
        }
        return Page();
    }
}