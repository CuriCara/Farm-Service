using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BusinessLogic.Harvests.Model;

public class DailyFilterModel
{
    [DataType(DataType.Date)]
    public DateTime? FromDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ToDate { get; set; }

    public int? UserId { get; set; }

    public List<SelectListItem> Users { get; set; } = new();

    public List<HarvestModel> Results { get; set; } = new();
}