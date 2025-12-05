using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Farm")]
public class Farm : BaseEntity
{
    public string Name { get; set; } = default;
    public string Address { get; set; } = default;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public List<Harvest> Harvests { get; set; } = new();

    public ICollection<FarmStorage> Storages { get; set; } = new List<FarmStorage>();
}