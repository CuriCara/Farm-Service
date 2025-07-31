using DataAccess.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

public class EditModelAcc : PageModel
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public EditModelAcc(UserManager<User> userManager, SignInManager<User> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required]
        [Display(Name = "Имя пользователя")]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }
        
        [Display(Name = "О себе")]
        public string? Description { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Старый пароль")]
        public string? OldPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Новый пароль")]
        public string? NewPassword { get; set; }
        
        [DataType(DataType.Password)]
        [Display(Name = "Повторите новый пароль")]
        [Compare("NewPassword", ErrorMessage = "Пароли не совпадают")]
        public string? ConfirmNewPassword { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("Пользователь не найден");
        }

        Input = new InputModel
        {
            UserName = user.UserName,
            Email = user.Email,
            Description = user.Description
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound("Пользователь не найден");
        
        var userNameResult = await _userManager.SetUserNameAsync(user, Input.UserName);
        var emailResult = await _userManager.SetEmailAsync(user, Input.Email);

        if (!userNameResult.Succeeded || !emailResult.Succeeded)
        {
            foreach (var error in userNameResult.Errors.Concat(emailResult.Errors))
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }
        
        if (!string.IsNullOrWhiteSpace(Input.OldPassword))
        {
            if (string.IsNullOrWhiteSpace(Input.NewPassword) || string.IsNullOrWhiteSpace(Input.ConfirmNewPassword))
            {
                ModelState.AddModelError(string.Empty, "Введите новый пароль и подтверждение.");
                return Page();
            }

            var passwordChangeResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!passwordChangeResult.Succeeded)
            {
                foreach (var error in passwordChangeResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }
        
        user.Description = Input.Description;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Данные успешно обновлены.";
        return RedirectToPage("Profile");
    }

}
