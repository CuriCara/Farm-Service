namespace Service.Controllers.Entity;

public class RegisterUserRequest
{
    public string Name { get; set; }
    public string Surname { get; set; }
    
    public string PasswordHash { get; set; }
    public string Email { get; set; }
    
    public int RoleId { get; set; }
}