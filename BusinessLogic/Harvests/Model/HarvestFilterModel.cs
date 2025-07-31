using Microsoft.AspNetCore.Mvc.Rendering;

namespace BusinessLogic.Harvests.Model
{
    public class HarvestFilterModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? ProductId { get; set; }

        public List<SelectListItem> Products { get; set; } = new();
        public List<HarvestModel> Results { get; set; } = new();
    }
}