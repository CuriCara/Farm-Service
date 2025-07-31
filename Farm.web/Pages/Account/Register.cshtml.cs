using System.ComponentModel.DataAnnotations;
using BusinessLogic.Authorization;
using BusinessLogic.Authorization.Exceptions;
using DataAccess.Entity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class RegisterModel : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly AuthProvider _authProvider;
    public RegisterModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    //    _authProvider = authProvider;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        public string Email { get; set; }

        [Required]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string PasswordDouble { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Неправельно введены данные");
            return Page();
        }

        var existingUser = await _userManager.FindByEmailAsync(Input.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(string.Empty, "Пользователь с таким Email уже есть.");
            return Page();
        }
        
        User user = new User { 
            UserName = Input.UserName, 
            Email = Input.Email,
            CreationTime = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            ModificationTime = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            ExternalId = Guid.NewGuid()
        };
        // var res = await _authProvider.RegisterUser(Input.Email, Input.UserName, Input.Password);
        // if (res != null)
        // {
        //     return Page();
        // }
        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Успешное создание.");
            return RedirectToPage("Login");
        }

        foreach (var err in result.Errors) ModelState.AddModelError(string.Empty, err.Description);
        return Page();
    }
}