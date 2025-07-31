namespace BusinessLogic.Users.Model;

public class CreateUserModel
{
    public string UserName { get; set; }
    
    public string Email { get; set; }
    
    public string PasswordHash { get; set; }
    
    public int RoleId { get; set; }
}