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
}
