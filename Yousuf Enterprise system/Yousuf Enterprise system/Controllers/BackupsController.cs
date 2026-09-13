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

    public BackupsController(ApplicationDbContext db, IBackupService backup)
    {
        _db = db;
        _backup = backup;
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
        TempData["Message"] = "Backup completed (local archive). Configure Google Drive for cloud upload.";
        return RedirectToAction(nameof(Index));
    }
}
