using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.ViewModels;

namespace Yousuf_Enterprise_system.Controllers;

// Roles/permissions management is always Super Admin-only, hardcoded — not gated through the
// module-permission system it configures, to avoid anyone being able to edit their own way in.
[Authorize(Roles = AppRoles.SuperAdmin)]
public class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public RolesController(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
        var userCounts = new Dictionary<string, int>();
        foreach (var role in roles)
        {
            userCounts[role.Id] = (await _userManager.GetUsersInRoleAsync(role.Name!)).Count;
        }
        ViewBag.UserCounts = userCounts;
        return View(roles);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Message"] = "Role name is required.";
            return RedirectToAction(nameof(Index));
        }

        if (await _roleManager.RoleExistsAsync(name))
        {
            TempData["Message"] = $"Role '{name}' already exists.";
            return RedirectToAction(nameof(Index));
        }

        await _roleManager.CreateAsync(new IdentityRole(name));
        TempData["Message"] = $"Role '{name}' created. Set its permissions below.";
        return RedirectToAction(nameof(Permissions), new { roleId = (await _roleManager.FindByNameAsync(name))!.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return NotFound();
        }

        if (role.Name is AppRoles.SuperAdmin or AppRoles.Accountant)
        {
            TempData["Message"] = "The built-in Super Admin and Accountant roles can't be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
        {
            TempData["Message"] = $"Can't delete '{role.Name}' — {usersInRole.Count} user(s) still have this role.";
            return RedirectToAction(nameof(Index));
        }

        await _roleManager.DeleteAsync(role);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Permissions(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return NotFound();
        }

        var existing = await _db.RolePermissions.AsNoTracking()
            .Where(p => p.RoleId == roleId)
            .ToDictionaryAsync(p => p.Module);

        var rows = Modules.All.Select(module => existing.TryGetValue(module, out var p)
            ? new RolePermissionRow { Module = module, CanView = p.CanView, CanEdit = p.CanEdit }
            : new RolePermissionRow { Module = module }).ToList();

        ViewBag.Role = role;
        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(string roleId, List<RolePermissionRow> rows)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return NotFound();
        }

        var existing = await _db.RolePermissions.Where(p => p.RoleId == roleId).ToListAsync();

        foreach (var row in rows)
        {
            var record = existing.FirstOrDefault(p => p.Module == row.Module);
            if (record is null)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    Module = row.Module,
                    CanView = row.CanView,
                    CanEdit = row.CanEdit
                });
            }
            else
            {
                record.CanView = row.CanView;
                record.CanEdit = row.CanEdit;
            }
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = $"Permissions saved for '{role.Name}'.";
        return RedirectToAction(nameof(Permissions), new { roleId });
    }
}
