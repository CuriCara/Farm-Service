using DataAccess.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class ProfileModel : PageModel
{
    private readonly UserManager<User> _userManager;
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public string Description { get; set; }

    public ProfileModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        
        UserName = user?.UserName;
        Email = user?.Email;
        Description = user?.Description ?? "";
        
        var roles = await _userManager.GetRolesAsync(user);
        Role = roles.FirstOrDefault() ?? "У пользователя нету роли";
    }
}