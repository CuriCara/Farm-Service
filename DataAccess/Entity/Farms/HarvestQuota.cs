using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("HarvestQuota")]
public class HarvestQuota : BaseEntity
{
    public DateTime Date { get; set; }
    
    public int Quota { get; set; }
}