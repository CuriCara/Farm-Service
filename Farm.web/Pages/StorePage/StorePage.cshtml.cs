using System.ComponentModel.DataAnnotations;
using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Farm.web.Pages.StorePage;

[Authorize(Roles = "Admin")]
public class StorePageModel : PageModel
{
    private readonly DeliveryPlanProvider _planProvider;
    private readonly StoreProvider _storeProvider;

    public List<SelectListItem> StoresItems { get; set; } = new();
    public DeliveryPlan? Plan { get; set; }
    public List<DeliveryItem> Items { get; set; } = new();

    public StorePageModel(
        DeliveryPlanProvider planProvider,
        StoreProvider storeProvider)
    {
        _planProvider = planProvider;
        _storeProvider = storeProvider;
    }

    [BindProperty(SupportsGet = true)]
    public StoreInputModel Input { get; set; } = new();

    public class StoreInputModel
    {
        [Required]
        public int StoreId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }
    
    public async Task<IActionResult> OnGetAsync(int? storeId, DateOnly? date)
    {
        await LoadStoresAsync();

        if (Input.StoreId > 0 && Input.Date != default)
        {
            Plan = await _planProvider.GetByStoreAndDateAsync(Input.StoreId, Input.Date);

            if (Plan != null)
                Items = Plan.Items?.ToList() ?? new();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostShowAsync()
    {
        await LoadStoresAsync();

        if (!ModelState.IsValid)
            return Page();

        return RedirectToPage(new
        {
            storeId = Input.StoreId,
            date = Input.Date.ToString("yyyy-MM-dd")
        });
    }

    public async Task<IActionResult> OnPostCreatePlanAsync()
    {
        await LoadStoresAsync();

        if (!ModelState.IsValid)
            return Page();

        var existingPlan = await _planProvider.GetByStoreAndDateAsync(Input.StoreId,Input.Date);

        if (existingPlan != null)
        {
            //await _planProvider.CreateRandomByStoreAsync(existingPlan.Id);
            await _planProvider.DeleteAsync(existingPlan);
        }
        
        await _planProvider.CreateRandomByStoreAndDateAsync(Input.StoreId, Input.Date);

        return RedirectToPage(new
        {
            storeId = Input.StoreId,
            date = Input.Date.ToString("yyyy-MM-dd")
        });
    }
    
    private async Task LoadStoresAsync()
    {
        StoresItems = (await _storeProvider.GetAllAsync())
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name
            })
            .ToList();
    }
}
