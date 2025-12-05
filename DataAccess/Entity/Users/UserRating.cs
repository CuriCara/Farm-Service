using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity;

[Table("UserRating")]
public class UserRating : BaseEntity
{
    public int Mark { get; set; }
    
    public DateTime DateMark { get; set; }
    
    public int UserId { get; set; }
    
    [ForeignKey("UserId")]
    public User User { get; set; }
}