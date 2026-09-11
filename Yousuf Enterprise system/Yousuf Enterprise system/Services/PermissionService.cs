using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Services;

public interface IPermissionService
{
    Task<bool> HasAccessAsync(ClaimsPrincipal user, string module, bool requireEdit);
}

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;

    public PermissionService(ApplicationDbContext db, RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _roleManager = roleManager;
    }

    // Super Admin bypasses this entirely — it can do everything, regardless of what's
    // configured on the Roles > Permissions page.
    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string module, bool requireEdit)
    {
        if (user.IsInRole(AppRoles.SuperAdmin))
        {
            return true;
        }

        var roleNames = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roleNames.Count == 0)
        {
            return false;
        }

        var roleIds = await _roleManager.Roles
            .Where(r => r.Name != null && roleNames.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync();

        var permissions = await _db.RolePermissions
            .Where(p => roleIds.Contains(p.RoleId) && p.Module == module)
            .ToListAsync();

        return requireEdit
            ? permissions.Any(p => p.CanEdit)
            : permissions.Any(p => p.CanView || p.CanEdit);
    }
}
