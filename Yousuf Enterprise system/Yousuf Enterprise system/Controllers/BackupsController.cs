using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Extensions;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class BackupsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IBackupService _backup;
    private readonly IWebHostEnvironment _env;

    public BackupsController(ApplicationDbContext db, IBackupService backup, IWebHostEnvironment env)
    {
        _db = db;
        _backup = backup;
        _env = env;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 25)
    {
        var logs = await _db.BackupLogs.AsNoTracking().OrderByDescending(b => b.Timestamp).ToPagedResultAsync(page, pageSize);
        return View(logs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run()
    {
        await _backup.RunBackupAsync("manual");
        TempData["Message"] = "Backup created. Download it below and upload it to your configured Google Drive folder.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Download(int id)
    {
        var log = await _db.BackupLogs.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (log?.FileName is null)
        {
            return NotFound();
        }

        var path = Path.Combine(_env.ContentRootPath, "App_Data", "backups", log.FileName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, "application/zip", log.FileName);
    }
}
