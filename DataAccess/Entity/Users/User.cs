using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace DataAccess.Entity;

[Table("users")]
public class User : IdentityUser<int>, IBaseEntity
{
    public string? Description { get; set; }
    public Guid ExternalId { get; set; }
    public DateTime ModificationTime { get; set; }
    public DateTime CreationTime { get; set; }
    public List<Harvest> Harvests { get; set; }
    public List<UserRating> UserRatings { get; set; }
}