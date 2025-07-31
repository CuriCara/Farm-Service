using System.ComponentModel.DataAnnotations;

namespace DataAccess.Entity;

public abstract class BaseEntity : IBaseEntity
{
    [Key] public int Id { get; set; }
    
    public Guid ExternalId { get; set; }
    
    public DateTime CreationTime { get; set; }
    
    public DateTime ModificationTime { get; set; }
}