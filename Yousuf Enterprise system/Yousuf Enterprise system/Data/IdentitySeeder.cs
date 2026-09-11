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

        // Seed default module permissions so existing roles keep the access they had before the
        // permission system existed: full view+edit on every business module. Super Admin is
        // bypassed at runtime regardless, but is seeded too so its Permissions page isn't blank.
        await EnsureDefaultPermissionsAsync(roleManager, db, AppRoles.SuperAdmin);
        await EnsureDefaultPermissionsAsync(roleManager, db, AppRoles.Accountant);

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

    private static async Task EnsureDefaultPermissionsAsync(RoleManager<IdentityRole> roleManager, ApplicationDbContext db, string roleName)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return;
        }

        var existingModules = await db.RolePermissions
            .Where(p => p.RoleId == role.Id)
            .Select(p => p.Module)
            .ToListAsync();

        foreach (var module in Modules.All.Except(existingModules))
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                Module = module,
                CanView = true,
                CanEdit = true
            });
        }

        await db.SaveChangesAsync();
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
