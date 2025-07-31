using System.ComponentModel.DataAnnotations;
using DataAccess.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class LoginModel : PageModel
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;

    public LoginModel(SignInManager<User> signInManager, UserManager<User> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public LoginInputModel Input { get; set; }

    public class LoginInputModel
    {
        [Required]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        public string Email { get; set; }


        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // 1. Найти пользователя по email
        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Неверные учетные данные");
            return Page();
        }

        // 2. Вход по username
        var result = await _signInManager.PasswordSignInAsync(user.UserName, Input.Password, false, false);
        if (result.Succeeded)
        {
            return RedirectToPage("/Harvest/Index"); // можно изменить путь
        }

        ModelState.AddModelError(string.Empty, "Неверные учетные данные");
        return Page();
    }
}