using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.Accountant })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@yousuf.local", "Admin@12345", "Super Admin", AppRoles.SuperAdmin);
        await EnsureUserAsync(userManager, "accountant@yousuf.local", "Accountant@123", "Accountant", AppRoles.Accountant);

        if (!await db.SystemSettings.AnyAsync())
        {
            db.SystemSettings.Add(new SystemSetting
            {
                CompanyName = "Yousuf Enterprise",
                Address = string.Empty,
                ContactNumbers = string.Empty,
                DefaultGstPercentage = 18.00m
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string fullName,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true
            };
            await userManager.CreateAsync(user, password);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
