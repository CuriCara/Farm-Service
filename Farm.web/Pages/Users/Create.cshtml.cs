using BusinessLogic.Users.Manager;
using BusinessLogic.Users.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Farm.Web.Pages.Users;

public class CreateModel : PageModel
{
    private readonly IUserManager _userManager;

    public CreateModel(IUserManager userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public CreateUserModel User { get; set; } = new();

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        _userManager.CreateUser(User);
        return RedirectToPage("Index");
    }
}