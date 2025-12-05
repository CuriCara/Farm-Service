using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("UnitCategory")]
public class UnitCategory : BaseEntity
{
    [Required]
    public string Name { get; set; }

    public int? BaseUnitId { get; set; }

    [ForeignKey(nameof(BaseUnitId))]
    public UnitsOfMeasurement BaseUnit { get; set; }

    public List<UnitsOfMeasurement> Units { get; set; }
}