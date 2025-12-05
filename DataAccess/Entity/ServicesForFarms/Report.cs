using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("Report")]
public class Report : BaseEntity
{
    public DateTime DateReport { get; set; }
    
    public string GeneralProducts { get; set; }
    
    public bool SentEmail { get; set; }
    
    public int HarvestId { get; set; }
    
    [ForeignKey("HarvestId")]
    public Harvest Harvest { get; set; }
}