using BusinessLogic.Users.Manager;
using BusinessLogic.Users.Provider;
using BusinessLogic.Users.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Farm.Web.Pages.Users;

public class IndexModel : PageModel
{
    private readonly IUserProvider _userProvider;
    private readonly IUserManager _userManager;

    public IndexModel(IUserProvider userProvider, IUserManager userManager)
    {
        _userProvider = userProvider;
        _userManager = userManager;
    }

    public List<UserModel> Users { get; set; } = new();

    public void OnGet()
    {
        Users = _userProvider.GetUsers().ToList();
    }

    public IActionResult OnPostDelete(int id)
    {
        _userManager.DeleteUser(id);
        return RedirectToPage();
    }
}