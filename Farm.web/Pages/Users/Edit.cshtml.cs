using BusinessLogic.Users.Manager;
using BusinessLogic.Users.Model;
using BusinessLogic.Users.Provider;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Farm.Web.Pages.Users;

public class EditModel : PageModel
{
    private readonly IUserProvider _userProvider;
    private readonly IUserManager _userManager;

    public EditModel(IUserProvider userProvider, IUserManager userManager)
    {
        _userProvider = userProvider;
        _userManager = userManager;
    }

    [BindProperty]
    public UpdateUserModel User { get; set; } = new();

    public IActionResult OnGet(int id)
    {
        var user = _userProvider.GetInfo(id);
        if (user == null)
            return NotFound();

        User = new UpdateUserModel
        {
            Id = user.Id,
            UserName = user.UserName,
        };

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        _userManager.UpdateUser(User);
        return RedirectToPage("Index");
    }
}