using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize(Roles = AppRoles.Staff)]
public class VouchersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;

    public VouchersController(ApplicationDbContext db, IDocumentNumberService numbers)
    {
        _db = db;
        _numbers = numbers;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.FinancialVouchers.AsNoTracking().Include(v => v.Party).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(v =>
                v.VoucherNumber.Contains(q)
                || (v.Party != null && v.Party.FullName.Contains(q))
                || (v.ReferenceNumber != null && v.ReferenceNumber.Contains(q)));
        }

        ViewBag.Query = q;
        return View(await query.OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await FillListsAsync();
        return View("Form", new FinancialVoucher
        {
            VoucherNumber = await _numbers.NextAsync("VCH", db => db.FinancialVouchers.Select(v => v.VoucherNumber)),
            VoucherDate = DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FinancialVoucher voucher)
    {
        if (voucher.Type == VoucherType.ContraAdjustment)
        {
            voucher.ApprovedByAdmin = User.IsInRole(AppRoles.SuperAdmin);
            var party = await _db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == voucher.PartyId);
            if (party is null || party.Type != PartyType.Both)
            {
                ModelState.AddModelError(string.Empty, "Contra netting is only allowed for parties marked as Both (vendor and buyer).");
            }
        }
        else
        {
            voucher.ApprovedByAdmin = true;
        }

        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", voucher);
        }

        _db.FinancialVouchers.Add(voucher);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var voucher = await _db.FinancialVouchers.FindAsync(id);
        if (voucher is null)
        {
            return NotFound();
        }

        voucher.ApprovedByAdmin = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync()
    {
        ViewBag.Parties = new SelectList(await _db.Parties.OrderBy(p => p.FullName).ToListAsync(), "Id", "FullName");
    }
}
