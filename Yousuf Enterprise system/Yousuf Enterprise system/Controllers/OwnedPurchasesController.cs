using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Yousuf_Enterprise_system.Data;
using Yousuf_Enterprise_system.Extensions;
using Yousuf_Enterprise_system.Models;
using Yousuf_Enterprise_system.Services;

namespace Yousuf_Enterprise_system.Controllers;

[Authorize]
[ModulePermission(Modules.OwnedStock)]
public class OwnedPurchasesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentNumberService _numbers;

    public OwnedPurchasesController(ApplicationDbContext db, IDocumentNumberService numbers)
    {
        _db = db;
        _numbers = numbers;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 25)
    {
        var query = _db.OwnedPurchases.AsNoTracking()
            .Include(p => p.Vendor)
            .Include(p => p.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p =>
                p.GrnNumber.Contains(q)
                || (p.Vendor != null && p.Vendor.FullName.Contains(q))
                || (p.Product != null && p.Product.Name.Contains(q)));
        }

        ViewBag.Query = q;
        return View(await query.OrderByDescending(p => p.PurchaseDate).ToPagedResultAsync(page, pageSize));
    }

    public async Task<IActionResult> Create()
    {
        await FillListsAsync();
        return View("Form", new OwnedPurchase
        {
            GrnNumber = await _numbers.NextAsync("GRN", db => db.OwnedPurchases.Select(p => p.GrnNumber)),
            PurchaseDate = DateTime.Today
        });
    }

    [ModulePermission(Modules.OwnedStock, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OwnedPurchase purchase)
    {
        ValidatePurchase(purchase);
        Recalculate(purchase);
        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", purchase);
        }

        _db.OwnedPurchases.Add(purchase);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var purchase = await _db.OwnedPurchases.FindAsync(id);
        if (purchase is null)
        {
            return NotFound();
        }

        await FillListsAsync();
        return View("Form", purchase);
    }

    [ModulePermission(Modules.OwnedStock, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OwnedPurchase purchase)
    {
        if (id != purchase.Id)
        {
            return BadRequest();
        }

        ValidatePurchase(purchase);
        Recalculate(purchase);
        if (!ModelState.IsValid)
        {
            await FillListsAsync();
            return View("Form", purchase);
        }

        // The form doesn't carry the dismissed flag; keep it unless the due date changed, in which
        // case the new due date should remind again.
        var existing = await _db.OwnedPurchases.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.ReminderDismissed, p.DueDate })
            .FirstOrDefaultAsync();
        purchase.ReminderDismissed = existing is not null && existing.ReminderDismissed && existing.DueDate == purchase.DueDate;

        _db.Update(purchase);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [ModulePermission(Modules.OwnedStock, edit: true)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReminder(int id)
    {
        var purchase = await _db.OwnedPurchases.FindAsync(id);
        if (purchase is not null)
        {
            purchase.ReminderDismissed = true;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Index", "Home");
    }

    private void ValidatePurchase(OwnedPurchase purchase)
    {
        if (purchase.PurchaseDate.Date > DateTime.Today)
        {
            ModelState.AddModelError(nameof(purchase.PurchaseDate), "Purchase date can't be in the future.");
        }

        if (purchase.PaymentType == PaymentType.Credit && purchase.DueDate is null)
        {
            ModelState.AddModelError(nameof(purchase.DueDate), "Due date is required when payment is on credit.");
        }

        if (purchase.PaymentType != PaymentType.Credit)
        {
            purchase.DueDate = null;
        }
    }

    private static void Recalculate(OwnedPurchase purchase)
    {
        purchase.TotalProductCost = Math.Round(purchase.Quantity * purchase.RatePerUnit, 2);
        purchase.GrandTotalAmount = purchase.TotalProductCost + purchase.FreightLabourCharges;
        purchase.GrossWeight = purchase.Quantity + (purchase.TotalPackageWeight ?? 0);
    }

    private async Task FillListsAsync()
    {
        ViewBag.Vendors = new SelectList(
            await _db.Parties.Where(p => p.Type == PartyType.Vendor || p.Type == PartyType.Both).OrderBy(p => p.FullName).ToListAsync(),
            "Id", "FullName");
        ViewBag.Products = new SelectList(
            await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
        var settings = await _db.SystemSettings.AsNoTracking().FirstAsync();
        ViewBag.BagWeightKg = settings.BagWeightKg;
    }
}
