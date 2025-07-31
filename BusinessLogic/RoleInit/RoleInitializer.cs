using DataAccess.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogic.RoleInit;

public class RoleInitializer
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<Role>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new Role{Name = role});
        }

        var adminEmail = "admin@admin.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            var user = new User
            {
                UserName = "admin",
                Email = adminEmail,
                EmailConfirmed = true,
                CreationTime = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                ModificationTime = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                ExternalId = Guid.NewGuid()
            };

            var result = await userManager.CreateAsync(user, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Admin");
            }
        }
    }
}