namespace BusinessLogic.Users.Model;

public class UserFilterModel
{
    public string? userNamePart { get; set; }
    public string? EmailPart { get; set; }
    public DateTime? CreationTime { get; set; }
    public DateTime? ModificationTime { get; set; }
    public int? Role { get; set; }
}