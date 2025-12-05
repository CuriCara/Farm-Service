namespace Service.Controllers.Entity;

public class UserFilter
{
    public string? NamePart { get; set; }
    public string? EmailPart { get; set; }

    public DateTime? CreationTime { get; set; }
    public DateTime? ModificationTime { get; set; }
    
    public int? RoleId { get; set; }
}