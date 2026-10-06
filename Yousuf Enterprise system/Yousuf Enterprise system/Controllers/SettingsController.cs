using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SettingsController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _db.SystemSettings.FirstAsync();
        return View(settings);
    }

    private static readonly string[] AllowedLogoExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp" };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SystemSetting model, IFormFile? logo)
    {
        var settings = await _db.SystemSettings.FirstAsync();

        if (string.IsNullOrWhiteSpace(model.CompanyName))
        {
            ModelState.AddModelError(nameof(model.CompanyName), "Company name is required.");
        }

        string? ext = null;
        if (logo is { Length: > 0 })
        {
            ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
            if (!AllowedLogoExtensions.Contains(ext))
            {
                ModelState.AddModelError(string.Empty, $"Logo must be one of: {string.Join(", ", AllowedLogoExtensions)}.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.LogoPath = settings.LogoPath;
            return View(model);
        }

        settings.CompanyName = model.CompanyName;
        settings.Address = model.Address;
        settings.ContactNumbers = model.ContactNumbers;
        settings.DefaultGstPercentage = model.DefaultGstPercentage;
        settings.BagWeightKg = model.BagWeightKg;
        settings.GoogleDriveFolderLink = model.GoogleDriveFolderLink;

        if (logo is { Length: > 0 })
        {
            try
            {
                var dir = Path.Combine(_env.WebRootPath, "uploads", "company");
                Directory.CreateDirectory(dir);
                var fileName = $"logo{ext}";
                var path = Path.Combine(dir, fileName);
                await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    await logo.CopyToAsync(stream);
                }
                settings.LogoPath = $"uploads/company/{fileName}";
            }
            catch (IOException ex)
            {
                ModelState.AddModelError(string.Empty, $"Logo upload failed: {ex.Message}");
                model.LogoPath = settings.LogoPath;
                return View(model);
            }
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ExpenseTypes()
    {
        var types = await _db.ExpenseTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        var usage = await _db.Expenses.AsNoTracking()
            .GroupBy(e => e.ExpenseTypeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        ViewBag.Usage = usage;
        return View(types);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpenseType(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            TempData["Error"] = "Enter a name for the expense type.";
        }
        else if (name.Length > 100)
        {
            TempData["Error"] = "Expense type name can't be longer than 100 characters.";
        }
        else if (await _db.ExpenseTypes.AnyAsync(t => t.Name == name))
        {
            TempData["Error"] = $"Expense type \"{name}\" already exists.";
        }
        else
        {
            _db.ExpenseTypes.Add(new ExpenseType { Name = name });
            await _db.SaveChangesAsync();
            TempData["Message"] = $"Expense type \"{name}\" added.";
        }

        return RedirectToAction(nameof(ExpenseTypes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameExpenseType(int id, string? name)
    {
        name = name?.Trim();
        var type = await _db.ExpenseTypes.FindAsync(id);
        if (type is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            TempData["Error"] = "Enter a name (up to 100 characters).";
        }
        else if (await _db.ExpenseTypes.AnyAsync(t => t.Id != id && t.Name == name))
        {
            TempData["Error"] = $"Expense type \"{name}\" already exists.";
        }
        else
        {
            type.Name = name;
            await _db.SaveChangesAsync();
            TempData["Message"] = "Expense type renamed.";
        }

        return RedirectToAction(nameof(ExpenseTypes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleExpenseType(int id)
    {
        var type = await _db.ExpenseTypes.FindAsync(id);
        if (type is null)
        {
            return NotFound();
        }

        type.IsActive = !type.IsActive;
        await _db.SaveChangesAsync();
        TempData["Message"] = $"\"{type.Name}\" is now {(type.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(ExpenseTypes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExpenseType(int id)
    {
        var type = await _db.ExpenseTypes.FindAsync(id);
        if (type is null)
        {
            return NotFound();
        }

        if (await _db.Expenses.AnyAsync(e => e.ExpenseTypeId == id))
        {
            TempData["Error"] = $"\"{type.Name}\" is used by existing expenses, so it can't be deleted. Mark it inactive instead.";
        }
        else
        {
            _db.ExpenseTypes.Remove(type);
            await _db.SaveChangesAsync();
            TempData["Message"] = $"Expense type \"{type.Name}\" deleted.";
        }

        return RedirectToAction(nameof(ExpenseTypes));
    }
}
