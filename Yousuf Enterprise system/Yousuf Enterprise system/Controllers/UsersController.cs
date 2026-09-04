using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.ViewModels;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        if (!string.IsNullOrWhiteSpace(q))
        {
            users = users.Where(u =>
                (u.Email ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase)
                || u.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var rows = new List<UserListItem>();
        foreach (var user in users)
        {
            rows.Add(new UserListItem
            {
                User = user,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }

        ViewBag.Query = q;
        return View(rows);
    }

    public IActionResult Create() => View("Form", new UserFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            IsActive = model.IsActive
        };
        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Form", model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return View("Form", new UserFormViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Role = roles.FirstOrDefault() ?? AppRoles.Accountant,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null)
        {
            return NotFound();
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.FullName = model.FullName;
        user.IsActive = model.IsActive;
        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, model.Role);

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, model.Password);
        }

        return RedirectToAction(nameof(Index));
    }
}
