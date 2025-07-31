using BusinessLogic.Harvests.Provider;
using Microsoft.AspNetCore.Mvc;
using DataAccess.Entity;

namespace Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UnitsController : ControllerBase
{
    private readonly UnitsOfMeasurementProvider _unitsProvider;
    private readonly UnitCategoryProvider _categoryProvider;

    public UnitsController(
        UnitsOfMeasurementProvider unitsProvider,
        UnitCategoryProvider categoryProvider)
    {
        _unitsProvider = unitsProvider;
        _categoryProvider = categoryProvider;
    }
    
    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories()
    {
        var categories = await _categoryProvider.GetAllAsync();
        var result = categories.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name
        }).ToList();

        return Ok(result);
    }
    
    [HttpGet("by-category/{categoryId}")]
    public async Task<ActionResult<List<UnitDto>>> GetUnitsByCategory(int categoryId)
    {
        var allUnits = await _unitsProvider.GetAllAsync();
        var filtered = allUnits
            .Where(u => u.UnitCategoryId == categoryId)
            .Select(u => new UnitDto
            {
                Id = u.Id,
                UoM = u.UoM,
                ConversionFactor = u.ConversionFactor
            })
            .ToList();

        return Ok(filtered);
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class UnitDto
    {
        public int Id { get; set; }
        public string UoM { get; set; }
        public double ConversionFactor { get; set; }
    }
}