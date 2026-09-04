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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SystemSetting model, IFormFile? logo)
    {
        var settings = await _db.SystemSettings.FirstAsync();
        settings.CompanyName = model.CompanyName;
        settings.Address = model.Address;
        settings.ContactNumbers = model.ContactNumbers;
        settings.DefaultGstPercentage = model.DefaultGstPercentage;

        if (logo is { Length: > 0 })
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "company");
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(logo.FileName);
            var path = Path.Combine(dir, "logo" + ext);
            await using var stream = System.IO.File.Create(path);
            await logo.CopyToAsync(stream);
            settings.LogoPath = "uploads/company/logo" + ext;
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }
}
