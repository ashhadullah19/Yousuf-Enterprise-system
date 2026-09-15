using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Extensions;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.Dasti)]
public class DastiController : Controller
{
    private readonly ApplicationDbContext _db;

    public DastiController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, DastiType? type, DateTime? from, DateTime? to, int page = 1, int pageSize = 25)
    {
        var query = _db.DastiEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(e => e.Name.Contains(q) || (e.Detail != null && e.Detail.Contains(q)));
        }

        if (type.HasValue) query = query.Where(e => e.Type == type.Value);
        if (from.HasValue) query = query.Where(e => e.Date >= from.Value.Date);
        if (to.HasValue) query = query.Where(e => e.Date <= to.Value.Date);

        ViewBag.Query = q;
        ViewBag.Type = (int?)type;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.TotalDeposits = await query.Where(e => e.Type == DastiType.CashDeposit).SumAsync(e => (decimal?)e.Amount) ?? 0m;
        ViewBag.TotalWithdrawals = await query.Where(e => e.Type == DastiType.CashWithdrawal).SumAsync(e => (decimal?)e.Amount) ?? 0m;

        return View(await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .ToPagedResultAsync(page, pageSize));
    }

    public IActionResult Create() => View("Form", new DastiEntry());

    [ModulePermission(Modules.Dasti, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DastiEntry entry)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", entry);
        }

        _db.DastiEntries.Add(entry);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var entry = await _db.DastiEntries.FindAsync(id);
        return entry is null ? NotFound() : View("Form", entry);
    }

    [ModulePermission(Modules.Dasti, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DastiEntry entry)
    {
        if (id != entry.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", entry);
        }

        _db.Update(entry);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.DastiEntries.FindAsync(id);
        if (entry is not null)
        {
            _db.DastiEntries.Remove(entry);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
