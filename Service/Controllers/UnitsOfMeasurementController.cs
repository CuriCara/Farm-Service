using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;
using Microsoft.AspNetCore.Mvc;

namespace Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UnitsOfMeasurementController : ControllerBase
{
    private readonly UnitsOfMeasurementProvider _unitsProvider;

    public UnitsOfMeasurementController(UnitsOfMeasurementProvider unitsProvider)
    {
        _unitsProvider = unitsProvider;
    }

    [HttpGet]
    public async Task<ActionResult<List<UnitsOfMeasurement>>> GetAll()
    {
        var units = await _unitsProvider.GetAllAsync();
        return Ok(units);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UnitsOfMeasurement>> GetById(int id)
    {
        var unit = await _unitsProvider.GetByIdAsync(id);
        if (unit == null) return NotFound();
        return Ok(unit);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UnitsOfMeasurement unit)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _unitsProvider.AddAsync(unit);
        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UnitsOfMeasurement updatedUnit)
    {
        if (id != updatedUnit.Id) return BadRequest();
        await _unitsProvider.UpdateAsync(updatedUnit);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var unit = await _unitsProvider.GetByIdAsync(id);
        if (unit == null) return NotFound();
        await _unitsProvider.DeleteAsync(unit);
        return Ok();
    }
}